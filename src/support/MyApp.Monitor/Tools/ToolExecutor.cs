using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MyApp.Monitor.Masking;

namespace MyApp.Monitor.Tools;

/// <summary>
/// The only way a tool runs (operations.md 7.3): validation, read-only enforcement, budgets, duplicates, masking and the audit line.
/// Constraints C01 to C06 and C10 (section 9) are enforced here.
/// </summary>
public sealed class ToolExecutor
{
    private const int MinUsefulChars = 40;

    private readonly ToolCatalog _catalog;
    private readonly EntityCatalog _entities;
    private readonly ConstraintSet _constraints;
    private readonly Masker _masker;
    private readonly ToolBindings _bindings;
    private readonly Dictionary<(string Family, string Kind), IToolImplementation> _implementations;
    private readonly IReadOnlyList<IToolAuditSink> _sinks;
    private readonly TimeProvider _time;
    private readonly ILogger? _logger;
    private readonly SchemaSubsetValidatorFactory _validators;

    public ToolExecutor(
        ToolCatalog catalog, EntityCatalog entities, ConstraintSet constraints, Masker masker, ToolBindings bindings,
        IEnumerable<IToolImplementation> implementations, IEnumerable<IToolAuditSink> sinks, TimeProvider time, ILogger? logger = null)
    {
        _catalog = catalog;
        _entities = entities;
        _constraints = constraints;
        _masker = masker;
        _bindings = bindings;
        _implementations = implementations.ToDictionary(i => (i.Family, i.SourceKind));
        _sinks = sinks.ToList();
        _time = time;
        _logger = logger;
        _validators = new SchemaSubsetValidatorFactory(entities);
    }

    public ToolCatalog Catalog => _catalog;

    /// <summary>Tools the active profile offers: exactly the keys of <c>tool_sources</c>.</summary>
    public IEnumerable<ToolDefinition> OfferedTools => _catalog.Tools.Where(t => _bindings.For(t.Name) is not null);

    public async Task<ToolEnvelope> ExecuteAsync(ToolRequest request, CancellationToken ct = default)
    {
        var started = Stopwatch.StartNew();
        var at = _time.GetUtcNow();
        var session = request.Session;
        var call = session.NextCall();
        var binding = _bindings.For(request.Tool);
        var tool = _catalog.Find(request.Tool);
        var args = request.Arguments ?? new JsonObject();

        async Task<ToolEnvelope> Finish(ToolEnvelope e, int outputChars = 0)
        {
            var row = new ToolAuditRow(
                at, session.IncidentId, request.Caller, request.Tool, MonitorJson.ToCompact(_masker.MaskArguments(args)),
                binding?.Id, e.Status, e.Rows, outputChars, started.ElapsedMilliseconds, e.Error);
            await AuditAsync(row).ConfigureAwait(false);
            return e;
        }
        Task<ToolEnvelope> Refuse(string status, string error, string? evidence = null) =>
            Finish(new ToolEnvelope(request.Tool, call, status, evidence ?? session.AllocateEvidence(), binding?.Id, 0, false, 0, null, error));

        // C05, C10: only catalog tools, only the profile's sources
        if (tool is null) return await Refuse(ToolStatus.Denied, "unknown tool");
        if (!_constraints.AllowedAccess.Contains(tool.Access)) return await Refuse(ToolStatus.Denied, $"access '{tool.Access}' is not allowed (C01)");
        if (binding is null) return await Refuse(ToolStatus.Denied, "tool is not offered by the active profile (C10)");
        if (!_implementations.TryGetValue((tool.Family, binding.Kind), out var impl) || tool.ImplementationFor(binding.Kind) is not { } settings)
            return await Refuse(ToolStatus.Denied, $"no implementation of '{tool.Name}' for source kind '{binding.Kind}'");

        // validation against the tool's schema
        var errors = _validators.For(tool).Validate(args);
        if (errors.Count > 0) return await Refuse(ToolStatus.Denied, Shorten("invalid arguments: " + string.Join("; ", errors.Take(3)), 300));

        // duplicates
        var key = tool.Name + "\n" + Canonical(args);
        if (session.EarlierEvidence(key) is { } earlier)
            return await Refuse(ToolStatus.Duplicate, "same tool with the same arguments already ran in this incident", earlier);

        // budgets (7.3, C03)
        if (session.CallsOf(tool.Name) >= tool.Limits.MaxCallsPerIncident)
            return await Refuse(ToolStatus.BudgetExceeded, $"at most {tool.Limits.MaxCallsPerIncident} calls of '{tool.Name}' per incident");
        if (_constraints.MaxToolCalls is { } maxCalls && session.ExecutedCalls >= maxCalls)
            return await Refuse(ToolStatus.BudgetExceeded, $"at most {maxCalls} tool calls per incident (C03)");
        if (_constraints.MaxOutputChars is { } maxTotal && maxTotal - session.OutputChars < MinUsefulChars)   // nothing useful fits any more
            return await Refuse(ToolStatus.BudgetExceeded, $"at most {maxTotal} characters of results per incident (C03)");

        var lookback = tool.Limits.LookbackHours;
        if (lookback is not null && _constraints.MaxLookbackHours is { } cap) lookback = Math.Min(lookback.Value, cap);   // C04
        var maxChars = tool.Limits.MaxOutputChars;
        if (_constraints.MaxOutputChars is { } totalCap) maxChars = (int)Math.Max(0, Math.Min(maxChars, totalCap - session.OutputChars));
        var limits = new EffectiveLimits(tool.Limits.MaxRows, tool.Limits.TimeoutSeconds, maxChars, lookback);

        var evidence = session.BeginExecution(tool.Name, key);
        var context = new ToolContext(tool, (JsonObject)args.DeepClone(), binding, (JsonObject)settings.DeepClone(), limits);

        ToolResult result;
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(limits.TimeoutSeconds));
            try { result = await impl.ExecuteAsync(context, cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return await Finish(new ToolEnvelope(tool.Name, call, ToolStatus.Timeout, evidence, binding.Id, 0, false, 0, null, $"no answer within {limits.TimeoutSeconds} seconds"));
            }
            catch (ToolArgumentException ex)
            {
                return await Finish(new ToolEnvelope(tool.Name, call, ToolStatus.Denied, evidence, binding.Id, 0, false, 0, null, Shorten(ex.Message, 300)));
            }
            catch (ToolException ex)
            {
                return await Finish(new ToolEnvelope(tool.Name, call, ToolStatus.Error, evidence, binding.Id, 0, false, 0, null, Shorten(ex.Message, 300)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "Tool {Tool} failed", tool.Name);
                return await Finish(new ToolEnvelope(tool.Name, call, ToolStatus.Error, evidence, binding.Id, 0, false, 0, null, "the source failed: " + ex.GetType().Name));
            }
        }

        // masking happens before the envelope leaves the executor (8.2)
        var entityName = MonitorJson.ScalarText(args["entity"]);
        var masked = _masker.MaskToolData(result.Data, tool.MaskingSection, entityName);
        var data = masked.Data;
        var rows = result.Rows;
        var truncated = result.Truncated;

        if (TrimRows(data, tool.Limits.MaxRows, ref rows)) truncated = true;
        var chars = CutToBudget(ref data, limits.MaxOutputChars, ref truncated);
        session.AddOutput(chars);

        var status = rows == 0 ? ToolStatus.Empty : ToolStatus.Ok;
        return await Finish(new ToolEnvelope(tool.Name, call, status, evidence, binding.Id, rows, truncated, masked.FieldsDropped, data, null), chars);
    }

    private async Task AuditAsync(ToolAuditRow row)
    {
        _logger?.Log(LogLevel.Information, new EventId(1, "monitor.tool.call"),
            "Tool call {Tool} by {Caller} on {Source}: {Status}, {Rows} rows, {Chars} characters, {Duration} ms",
            row.Tool, row.Caller, row.SourceId, row.Status, row.Rows, row.OutputChars, row.DurationMs);
        foreach (var sink in _sinks)
        {
            try { await sink.WriteAsync(row, CancellationToken.None).ConfigureAwait(false); }
            catch (Exception ex) { _logger?.LogError(ex, "Writing the tool audit row failed"); }
        }
    }

    /// <summary>Caps top-level arrays of <c>data</c> at <paramref name="maxRows"/>.</summary>
    private static bool TrimRows(JsonNode? data, int maxRows, ref int rows)
    {
        var trimmed = false;
        if (data is JsonObject obj)
            foreach (var kv in obj)
                if (kv.Value is JsonArray arr && arr.Count > maxRows)
                {
                    while (arr.Count > maxRows) arr.RemoveAt(arr.Count - 1);
                    trimmed = true;
                }
        if (rows > maxRows) rows = maxRows;
        return trimmed;
    }

    /// <summary>Cuts <c>data</c> to the character budget and returns its final length.</summary>
    private static int CutToBudget(ref JsonNode? data, int maxChars, ref bool truncated)
    {
        if (data is null) return 0;
        var length = MonitorJson.ToCompact(data).Length;
        if (length <= maxChars) return length;
        truncated = true;
        if (data is JsonObject obj)
        {
            foreach (var arr in obj.Select(kv => kv.Value).OfType<JsonArray>().ToList())
                while (arr.Count > 0 && MonitorJson.ToCompact(data).Length > maxChars) arr.RemoveAt(arr.Count - 1);
            length = MonitorJson.ToCompact(data).Length;
            if (length <= maxChars) return length;
        }
        if (maxChars < 40) { data = null; return 0; }
        var text = MonitorJson.ToCompact(data);
        var keep = Math.Max(0, maxChars - 40);
        data = new JsonObject { ["truncated_output"] = text[..Math.Min(keep, text.Length)] };
        return MonitorJson.ToCompact(data).Length;
    }

    private static string Canonical(JsonNode? node) => node switch
    {
        JsonObject o => "{" + string.Join(",", o.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"\"{kv.Key}\":{Canonical(kv.Value)}")) + "}",
        JsonArray a => "[" + string.Join(",", a.Select(Canonical)) + "]",
        null => "null",
        _ => node.ToJsonString(MonitorJson.Compact),
    };

    private static string Shorten(string s, int max) => s.Length <= max ? s : s[..max];
}

internal sealed class SchemaSubsetValidatorFactory(EntityCatalog entities)
{
    private readonly Dictionary<string, SchemaSubsetValidator> _cache = new(StringComparer.Ordinal);

    public SchemaSubsetValidator For(ToolDefinition tool)
    {
        lock (_cache)
        {
            if (!_cache.TryGetValue(tool.Name, out var v))
                _cache[tool.Name] = v = new SchemaSubsetValidator(tool.Parameters, source => source == "entities" ? entities.Names : null);
            return v;
        }
    }
}
