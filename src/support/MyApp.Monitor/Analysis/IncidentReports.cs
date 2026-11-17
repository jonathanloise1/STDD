using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyApp.Monitor.Masking;
using MyApp.Monitor.Rules;
using MyApp.Monitor.Store;
using static MyApp.Monitor.Store.SqliteDatabase;

namespace MyApp.Monitor.Analysis;

/// <summary>Action names of <c>actions_taken</c> (docs/support/incident-report.schema.json).</summary>
public static class ReportActions
{
    public const string Created = "created";
    public const string DedupHit = "dedup_hit";
    public const string SeverityRaised = "severity_raised";
    public const string RuleChecksRun = "rule_checks_run";
    public const string InvestigationQueued = "investigation_queued";
    public const string InvestigationDone = "investigation_done";
    public const string InvestigationAbstained = "investigation_abstained";
    public const string InvestigationFailed = "investigation_failed";
    public const string EmailSent = "email_sent";
    public const string EmailDraft = "email_draft";
    public const string TicketCreated = "ticket_created";
    public const string TicketDraft = "ticket_draft";
    public const string NotificationFailed = "notification_failed";
    public const string Suppressed = "suppressed";
    public const string Acknowledged = "acknowledged";
    public const string Closed = "closed";
}

/// <summary>
/// Builds and updates incident reports (operations.md 10.1). The report is the one object that is stored, shown, rendered into
/// e-mail and ticket, and consumed by <c>fix.agent.md</c>; it validates against <c>incident-report.schema.json</c>.
/// </summary>
public static class IncidentReports
{
    public const int MaxEvidence = 40;
    public const int MaxAppendedEvidence = 20;
    public const int MaxTraceIds = 50;
    public const int MaxActions = 200;
    private const int TextMax = 1000;

    private static readonly Regex TraceIdPattern = new("^[0-9a-f]{32}$", RegexOptions.CultureInvariant);
    private static readonly Regex AreaPattern = new("^[a-z]+(-[a-z]+)*$", RegexOptions.CultureInvariant);
    private static readonly Regex StoryPattern = new(@"^US-[A-Z]{2,8}-\d{2}$", RegexOptions.CultureInvariant);
    private static readonly Regex TestCasePattern = new(@"^[A-Z]{2,8}-[A-Z]{2,12}-\d{3}$", RegexOptions.CultureInvariant);
    private static readonly Regex LogKeyPattern = new(@"^[a-z]{2,8}\.[a-z]{2,12}\.[a-z]+(_[a-z]+)*$", RegexOptions.CultureInvariant);
    private static readonly Regex TemplateIdPattern = new("^t-[0-9a-f]{12}$", RegexOptions.CultureInvariant);

    public static string MonitorVersion { get; } = ComputeVersion();

    private static string ComputeVersion()
    {
        var info = typeof(IncidentReports).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plus = info.IndexOf('+');
        return plus >= 0 ? info[..plus] : info;
    }

    /// <summary>The report of a new incident, from the rule engine's result. Every text written here comes from masked sources or the pack.</summary>
    public static JsonObject Build(RuleResult rule, string id, DateTimeOffset now, string packVersion, Masker masker)
    {
        var e = rule.Escalation;
        var record = e.Record;
        var sig = rule.Signature;

        JsonNode? Opt(string? value, Regex pattern) => value is not null && pattern.IsMatch(value) ? JsonValue.Create(value) : null;

        var signature = new JsonObject();
        if (sig is not null)
        {
            signature["kind"] = sig.Kind;
            signature["id"] = Cut(sig.Id, 100);
            signature["log_key"] = sig.IsKeyed ? Opt(sig.Id, LogKeyPattern) : null;
            signature["template_id"] = sig.IsKeyed ? null : Opt(sig.Id, TemplateIdPattern);
            signature["template"] = sig.IsKeyed ? null : Cut(sig.Match.Template, 300);
        }
        else
        {
            var template = e.Match.Template;
            signature["kind"] = "none";
            signature["id"] = e.Match.Kind == MatchKind.UnknownKey ? Cut(e.Match.Id, 100) : null;
            signature["log_key"] = e.Match.Kind == MatchKind.UnknownKey ? Opt(e.Match.Id, LogKeyPattern) : null;
            signature["template_id"] = template is null ? null : Opt(template.Id, TemplateIdPattern);
            signature["template"] = template is null ? null : Cut(template.Discriminator, 300);
        }

        var traceIds = new JsonArray();
        foreach (var t in rule.TraceIds.Where(t => TraceIdPattern.IsMatch(t)).Distinct().Take(MaxTraceIds)) traceIds.Add(t);
        var entities = new JsonArray();
        foreach (var en in rule.Entities.Take(20)) entities.Add(new JsonObject { ["entity"] = en.Entity, ["id"] = Cut(en.Id, 100) });

        var evidence = new JsonArray();
        foreach (var item in rule.Evidence.Take(MaxEvidence))
        {
            var reference = item.Id == "E1" ? masker.MaskFreeText(item.Reference, TextMax) : Cut(item.Reference, TextMax);
            evidence.Add(new JsonObject
            {
                ["id"] = item.Id, ["source"] = Cut(item.Source, 64), ["reference"] = reference,
                ["observed_at"] = Iso(item.ObservedAt), ["excerpt"] = Cut(item.Excerpt, TextMax),
            });
        }

        JsonNode? Verdict() => rule.Verdict is null ? null : new JsonObject
        {
            ["cause_category"] = rule.Verdict.CauseCategory,
            ["cause"] = Cut(rule.Verdict.Cause, 300),
            ["confidence"] = rule.Verdict.Confidence,
            ["evidence_refs"] = new JsonArray(rule.Verdict.EvidenceRefs.Take(10).Select(r => (JsonNode?)JsonValue.Create(r)).ToArray()),
        };

        JsonNode? Runbook()
        {
            if (sig is null) return null;
            var checks = new JsonArray();
            foreach (var c in rule.Checks.Take(10))
                checks.Add(new JsonObject { ["tool"] = Cut(c.Tool, 64), ["expect"] = Cut(c.Expect, 200), ["passed"] = c.Passed, ["evidence_id"] = c.EvidenceId ?? "E1" });
            return new JsonObject { ["anchor"] = Cut(sig.Runbook.Anchor, 100), ["path"] = Cut(sig.Runbook.Path, 200), ["checks"] = checks };
        }

        var actions = new JsonArray { Action(now, ReportActions.Created, null) };
        if (rule.Checks.Count > 0)
            actions.Add(Action(now, ReportActions.RuleChecksRun, string.Join(",", rule.Checks.Select(c => c.EvidenceId ?? "-").Distinct())));

        return new JsonObject
        {
            ["schema_version"] = 1,
            ["id"] = id,
            ["fingerprint"] = e.Fingerprint,
            ["environment"] = Cut(record.Environment, 64),
            ["status"] = "open",
            ["trigger"] = e.Trigger,
            ["signature"] = signature,
            ["area"] = Opt(sig?.Area, AreaPattern),
            ["user_story"] = Opt(sig?.UserStory, StoryPattern),
            ["test_case"] = Opt(sig?.TestCase, TestCasePattern),
            ["severity"] = e.Severity,
            ["first_seen"] = Iso(record.Timestamp),
            ["last_seen"] = Iso(record.Timestamp),
            ["count"] = Math.Max(1, e.FingerprintWindowCount),
            ["rate_per_hour"] = Math.Round(e.FingerprintRatePerHour, 2),
            ["previous_incident_id"] = e.PreviousIncidentId,
            ["correlation"] = new JsonObject { ["trace_ids"] = traceIds, ["entities"] = entities },
            ["evidence"] = evidence,
            ["rule_verdict"] = Verdict(),
            ["model_verdict"] = null,
            ["investigation"] = null,
            ["model"] = null,
            ["reproduction"] = null,
            ["runbook"] = Runbook(),
            ["actions_taken"] = actions,
            ["outcome"] = null,
            ["pack_version"] = Cut(packVersion, 100),
            ["monitor_version"] = Cut(MonitorVersion, 100),
        };
    }

    /// <summary>
    /// A record folded into an open incident (6.8): <c>last_seen</c>, <c>count</c>, <c>rate_per_hour</c>, the trace id (at most 50) and
    /// the record as evidence (at most 20 items). A severity raise is recorded as an action.
    /// </summary>
    public static IncidentChange Append(JsonObject report, Escalation e, DateTimeOffset now, Masker masker)
    {
        var record = e.Record;
        var events = new List<(string, JsonNode)>();
        report["count"] = (int)MonitorJson.Get(report, "count")!.GetValue<long>() + 1;
        var last = ParseIso(MonitorJson.ScalarText(report["last_seen"])!);
        if (record.Timestamp > last) report["last_seen"] = Iso(record.Timestamp);
        var first = ParseIso(MonitorJson.ScalarText(report["first_seen"])!);
        if (record.Timestamp < first) report["first_seen"] = Iso(record.Timestamp);
        report["rate_per_hour"] = Math.Round(e.FingerprintRatePerHour, 2);

        if (record.TraceId is not null && TraceIdPattern.IsMatch(record.TraceId) && report["correlation"]?["trace_ids"] is JsonArray traces
            && traces.Count < MaxTraceIds && traces.All(t => MonitorJson.ScalarText(t) != record.TraceId))
            traces.Add(record.TraceId);

        var evidence = (JsonArray)report["evidence"]!;
        var appended = evidence.Count(x => MonitorJson.ScalarText(x?["source"]) == "signal" && MonitorJson.ScalarText(x?["id"]) != "E1");
        if (evidence.Count < MaxEvidence && appended < MaxAppendedEvidence)
        {
            var next = evidence.Select(x => int.Parse(MonitorJson.ScalarText(x?["id"])![1..])).DefaultIfEmpty(0).Max() + 1;
            if (next < 1000)
                evidence.Add(new JsonObject
                {
                    ["id"] = "E" + next, ["source"] = "signal", ["reference"] = masker.MaskFreeText(RuleEngine.SignalReference(record), TextMax),
                    ["observed_at"] = Iso(record.Timestamp), ["excerpt"] = Cut(record.Message, TextMax),
                });
        }

        var current = MonitorJson.ScalarText(report["severity"])!;
        if (Severities.Rank(e.Severity) > Severities.Rank(current))
        {
            report["severity"] = e.Severity;
            AddAction(report, now, ReportActions.SeverityRaised, $"{current} to {e.Severity}");
            events.Add(("severity_raised", new JsonObject { ["from"] = current, ["to"] = e.Severity }));
        }
        else
        {
            // one dedup entry per hour keeps a noisy incident inside the schema's 200 actions
            var actions = (JsonArray)report["actions_taken"]!;
            var lastHit = actions.LastOrDefault(a => MonitorJson.ScalarText(a?["action"]) == ReportActions.DedupHit);
            var recent = lastHit is not null && now - ParseIso(MonitorJson.ScalarText(lastHit["at"])!) < TimeSpan.FromHours(1);
            if (!recent && actions.Count < MaxActions - 10) AddAction(report, now, ReportActions.DedupHit, null);
        }
        if (events.Count == 0)
            events.Add(("record_appended", new JsonObject { ["record_id"] = record.RecordId, ["at"] = Iso(record.Timestamp) }));
        return new IncidentChange(events);
    }

    public static JsonObject Action(DateTimeOffset at, string action, string? reference) => new()
    {
        ["at"] = Iso(at), ["action"] = action, ["reference"] = reference is null ? null : Cut(reference, 500),
    };

    public static void AddAction(JsonObject report, DateTimeOffset at, string action, string? reference)
    {
        var actions = (JsonArray)report["actions_taken"]!;
        if (actions.Count >= MaxActions) return;
        actions.Add(Action(at, action, reference));
    }

    public static string Cut(string? value, int max) => value is null ? "" : value.Length <= max ? value : value[..max];

    /// <summary>Applies the patterns of the masking policy to every text of a report received from another monitor (the second line of defence).</summary>
    public static void MaskAllText(JsonNode? node, Masker masker)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kv => kv.Key).ToList())
                {
                    if (obj[key] is JsonValue v && v.TryGetValue<string>(out var s) && NeedsMasking(key)) obj[key] = masker.MaskFreeText(s, int.MaxValue);
                    else MaskAllText(obj[key], masker);
                }
                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                    if (arr[i] is JsonValue v && v.TryGetValue<string>(out var s)) arr[i] = masker.MaskFreeText(s, int.MaxValue);
                    else MaskAllText(arr[i], masker);
                break;
        }
    }

    // identifiers and timestamps are validated by the schema; masking them would only corrupt them
    private static bool NeedsMasking(string key) => key is "excerpt" or "reference" or "cause" or "observed" or "expected" or "note" or "message" or "template"
        or "title" or "ticket" or "pull_request" or "closed_by" or "expect";
}
