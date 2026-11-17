using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Rules;

public sealed record EvidenceItem(string Id, string Source, string Reference, DateTimeOffset ObservedAt, string Excerpt);

public sealed record EntityRef(string Entity, string Id);

/// <summary>One rule check of the runbook, recorded in <c>runbook.checks</c> of the report.</summary>
public sealed record CheckResult(string Tool, string Expect, bool Passed, string? EvidenceId, string? Error);

public sealed record RuleVerdict(string CauseCategory, string Cause, double Confidence, IReadOnlyList<string> EvidenceRefs);

/// <summary>What the rule engine hands to the next pass: evidence, correlation, checks and the verdict (operations.md 6.9).</summary>
public sealed class RuleResult
{
    public Escalation Escalation { get; init; } = null!;
    public Signature? Signature { get; init; }
    public string? RunbookText { get; init; }
    public List<EvidenceItem> Evidence { get; } = [];
    public List<string> TraceIds { get; } = [];
    public List<EntityRef> Entities { get; } = [];
    public List<CheckResult> Checks { get; } = [];
    public RuleVerdict? Verdict { get; set; }
    /// <summary>The trigger is residual, or a runbook check failed: the evidence contradicts the runbook.</summary>
    public bool Residual { get; set; }
    public bool ChecksFailed => Checks.Any(c => !c.Passed);
    public string? RunbookAnchor => Signature?.Runbook.Anchor;
    public string? RunbookPath => Signature?.Runbook.Path;
}

/// <summary>
/// The rule engine (operations.md 6.9): for every escalation, before any model call, attach the signature and runbook text,
/// correlate through the executor with caller <c>rules</c>, run the runbook's rule checks, and build the rule verdict.
/// </summary>
public sealed class RuleEngine(ToolExecutor executor, EntityCatalog entities, TimeProvider time)
{
    public const string Caller = "rules";
    private const int ExcerptMax = 1000;
    private static readonly Regex Placeholder = new(@"\{signal\.([A-Za-z]+(?:\.[A-Za-z0-9_]+)?)\}", RegexOptions.CultureInvariant);

    public async Task<RuleResult> RunAsync(Escalation escalation, IncidentToolSession session, CancellationToken ct = default)
    {
        var sig = escalation.Signature;
        var record = escalation.Record;
        var result = new RuleResult { Escalation = escalation, Signature = sig, RunbookText = sig?.Runbook.Text };

        // 1. E1 is the triggering signal
        result.Evidence.Add(new EvidenceItem(
            "E1", "signal",
            Cut(SignalReference(record)), record.Timestamp, Cut(record.Message)));

        var cache = new Dictionary<string, ToolEnvelope>(StringComparer.Ordinal);

        // 2. correlate: the trace, then the entities named by fingerprint_by
        if (record.TraceId is not null)
        {
            result.TraceIds.Add(record.TraceId);
            await CallAsync(result, session, cache, "logs_by_correlation", new JsonObject { ["trace_id"] = record.TraceId }, ct).ConfigureAwait(false);
        }
        foreach (var name in sig?.FingerprintBy ?? [])
        {
            var entity = EntityOf(name);
            var id = record.Property(name);
            if (entity is null || string.IsNullOrEmpty(id)) continue;
            await CorrelateEntityAsync(result, session, cache, entity, id, ct).ConfigureAwait(false);
        }

        // 3. the runbook's rule checks
        var checkEvidence = new List<string>();
        foreach (var check in sig?.RuleChecks ?? [])
        {
            Expectation.TryParse(check.Expect, out var expectation, out _);
            if (!TryResolveArguments(check.Arguments, record, out var args, out var unresolved))
            {
                result.Checks.Add(new CheckResult(check.Tool, check.Expect, false, null, $"placeholder {unresolved} has no value"));
                continue;
            }
            Coerce(check.Tool, args);
            var envelope = await CallAsync(result, session, cache, check.Tool, args, ct).ConfigureAwait(false);
            var passed = envelope.IsSuccess && expectation is not null && expectation.Evaluate(envelope.Data);
            result.Checks.Add(new CheckResult(check.Tool, check.Expect, passed, envelope.EvidenceId, envelope.IsSuccess ? null : envelope.Error ?? envelope.Status));
            if (envelope.EvidenceId is not null) checkEvidence.Add(envelope.EvidenceId);
        }

        // a failed check makes the incident residual, whatever its trigger
        result.Residual = escalation.Residual || result.ChecksFailed;

        // 4. the rule verdict exists for a matched signature only
        if (sig is not null)
        {
            var refs = new List<string> { "E1" };
            refs.AddRange(checkEvidence.Distinct().Take(9));
            result.Verdict = new RuleVerdict(sig.CauseCategory, Cut(Meaning(sig.Runbook.Text), 300), result.ChecksFailed ? 0.5 : 1.0, refs);
        }
        return result;
    }

    private async Task CorrelateEntityAsync(
        RuleResult result, IncidentToolSession session, Dictionary<string, ToolEnvelope> cache, EntityDefinition entity, string id, CancellationToken ct)
    {
        if (!result.Entities.Contains(new EntityRef(entity.Name, id))) result.Entities.Add(new EntityRef(entity.Name, id));
        await CallAsync(result, session, cache, "sql_entity_read", new JsonObject { ["entity"] = entity.Name, ["id"] = id }, ct).ConfigureAwait(false);
        if (entity.AuditEntityType is not null)
            await CallAsync(result, session, cache, "audit_trail_by_entity", new JsonObject { ["entity"] = entity.Name, ["id"] = id }, ct).ConfigureAwait(false);
    }

    /// <summary>Correlates one more entity on an existing result (a manual incident opened from an entity search).</summary>
    public async Task AddEntityAsync(RuleResult result, IncidentToolSession session, string entityName, string id, CancellationToken ct = default)
    {
        var entity = entities.Entities.FirstOrDefault(e => e.Name == entityName);
        if (entity is null) return;
        await CorrelateEntityAsync(result, session, new Dictionary<string, ToolEnvelope>(StringComparer.Ordinal), entity, id, ct).ConfigureAwait(false);
    }

    private async Task<ToolEnvelope> CallAsync(
        RuleResult result, IncidentToolSession session, Dictionary<string, ToolEnvelope> cache, string tool, JsonObject args, CancellationToken ct)
    {
        var key = tool + "\n" + MonitorJson.ToCompact(args);
        if (cache.TryGetValue(key, out var earlier)) return earlier;   // the data of an earlier call of this run
        var envelope = await executor.ExecuteAsync(new ToolRequest(tool, args, Caller, session), ct).ConfigureAwait(false);
        cache[key] = envelope;
        if (envelope.EvidenceId is not null && envelope.Status != ToolStatus.Duplicate && result.Evidence.All(e => e.Id != envelope.EvidenceId))
        {
            result.Evidence.Add(new EvidenceItem(
                envelope.EvidenceId, tool, Cut($"{tool} {Describe(args)}"), time.GetUtcNow(),
                Cut(envelope.Data is null ? $"{envelope.Status}: {envelope.Error}" : MonitorJson.ToCompact(envelope.Data))));
        }
        return envelope;
    }

    /// <summary>What a person opens to find the triggering record: the source, the file when there is one, the key, the trace and the time.</summary>
    public static string SignalReference(Sources.LogRecord record)
    {
        var where = record.Origin is null ? record.SourceId : $"{record.SourceId}: {record.Origin}";
        var what = record.LogKey is not null ? $"log key {record.LogKey}" : record.EventName is not null ? $"event {record.EventName}" : $"category {record.Category}";
        var trace = record.TraceId is null ? "" : $", trace {record.TraceId}";
        return $"{where}, {what}{trace}, at {Sources.LogRecord.FormatTimestamp(record.Timestamp)}";
    }

    private EntityDefinition? EntityOf(string propertyName) =>
        entities.Entities.FirstOrDefault(e =>
            string.Equals(propertyName, string.Concat(e.Name.Split('_').Select(p => char.ToUpperInvariant(p[0]) + p[1..])) + "Id", StringComparison.OrdinalIgnoreCase));

    /// <summary>Replaces <c>{signal.TraceId}</c> and <c>{signal.Properties.Name}</c> with values of the triggering record.</summary>
    public static bool TryResolveArguments(JsonObject template, Sources.LogRecord record, out JsonObject resolved, out string? unresolved)
    {
        resolved = new JsonObject();
        unresolved = null;
        foreach (var kv in template)
        {
            if (kv.Value is JsonValue v && v.TryGetValue<string>(out var text) && text.Contains("{signal.", StringComparison.Ordinal))
            {
                string? missing = null;
                var replaced = Placeholder.Replace(text, m =>
                {
                    var value = SignalValue(record, m.Groups[1].Value);
                    if (value is null) missing ??= m.Value;
                    return value ?? "";
                });
                if (missing is not null) { unresolved = missing; return false; }
                resolved[kv.Key] = replaced;
            }
            else resolved[kv.Key] = kv.Value?.DeepClone();
        }
        return true;
    }

    private static string? SignalValue(Sources.LogRecord r, string path)
    {
        if (path.StartsWith("Properties.", StringComparison.Ordinal)) return r.Property(path["Properties.".Length..]);
        return path switch
        {
            "TraceId" => r.TraceId,
            "SpanId" => r.SpanId,
            "Category" => r.Category,
            "LogKey" => r.LogKey,
            "EventName" => r.EventName,
            "Version" => r.Version,
            "Host" => r.Host,
            "Service" => r.Service,
            "Environment" => r.Environment,
            "ExceptionType" => r.ExceptionType,
            "TopFrame" => r.TopFrame,
            _ => null,
        };
    }

    /// <summary>Runbook literals are typed by shape; a string parameter given a number is converted.</summary>
    private void Coerce(string tool, JsonObject args)
    {
        var def = executor.Catalog.Find(tool);
        if (def?.Parameters["properties"] is not JsonObject props) return;
        foreach (var kv in args.ToList())
        {
            var type = MonitorJson.ScalarText(props[kv.Key]?["type"]);
            if (type == "string" && kv.Value is JsonValue v && !v.TryGetValue<string>(out _)) args[kv.Key] = MonitorJson.ScalarText(v);
            if (type == "integer" && kv.Value is JsonValue s && s.TryGetValue<string>(out var text) && long.TryParse(text, out var l)) args[kv.Key] = l;
        }
    }

    public static string Meaning(string runbookText)
    {
        var lines = runbookText.Replace("\r\n", "\n").Split('\n');
        var start = Array.FindIndex(lines, l => l.Trim() == "### Meaning");
        if (start < 0) return "";
        var body = lines.Skip(start + 1).TakeWhile(l => !l.StartsWith("### ", StringComparison.Ordinal));
        return Regex.Replace(string.Join(' ', body.Select(l => l.Trim()).Where(l => l.Length > 0)), @"\s+", " ");
    }

    private static string Describe(JsonObject args) => string.Join(' ', args.Select(kv => $"{kv.Key}={MonitorJson.ScalarText(kv.Value)}"));

    private static string Cut(string s, int max = ExcerptMax) => s.Length <= max ? s : s[..max];
}
