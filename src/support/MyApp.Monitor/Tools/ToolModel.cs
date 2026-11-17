using System.Text.Json.Nodes;
using MyApp.Monitor.Pack;

namespace MyApp.Monitor.Tools;

public static class ToolStatus
{
    public const string Ok = "ok";
    public const string Empty = "empty";
    public const string Error = "error";
    public const string Denied = "denied";
    public const string Timeout = "timeout";
    public const string BudgetExceeded = "budget_exceeded";
    public const string Duplicate = "duplicate";
}

/// <summary>A profile source a tool is bound to: id, kind and the settings of the profile entry.</summary>
public sealed record SourceBinding(string Id, string Kind, JsonObject Settings)
{
    public const string PackId = "pack";
    public const string WorldId = "world";

    public string? GetString(string name) => MonitorJson.ScalarText(Settings[name]);

    public IReadOnlyList<string> GetStrings(string name) => Settings[name] switch
    {
        JsonArray a => a.Select(n => MonitorJson.ScalarText(n) ?? "").Where(s => s.Length > 0).ToList(),
        JsonValue v when MonitorJson.ScalarText(v) is { Length: > 0 } s => [s],
        _ => [],
    };
}

/// <summary>Which source answers which tool, from the profile's <c>tool_sources</c> (operations.md 12.1).</summary>
public sealed class ToolBindings
{
    private readonly Dictionary<string, SourceBinding> _byTool;

    public ToolBindings(IReadOnlyDictionary<string, SourceBinding> byTool) => _byTool = new(byTool, StringComparer.Ordinal);

    public IReadOnlyCollection<string> ToolNames => _byTool.Keys;
    public SourceBinding? For(string tool) => _byTool.GetValueOrDefault(tool);

    public static ToolBindings FromProfile(Profile profile)
    {
        var map = new Dictionary<string, SourceBinding>(StringComparer.Ordinal);
        foreach (var (tool, sourceId) in profile.ToolSources)
        {
            if (sourceId == SourceBinding.PackId) { map[tool] = new SourceBinding(SourceBinding.PackId, "pack", new JsonObject()); continue; }
            var source = profile.FindSource(sourceId) ?? throw new MonitorException($"tool '{tool}' is bound to unknown source '{sourceId}'");
            map[tool] = new SourceBinding(source.Id, source.Kind, (JsonObject)source.Settings.DeepClone());
        }
        return new ToolBindings(map);
    }

    /// <summary>Binds every tool to a simulated world directory (<c>tool --world</c>); <c>signature_lookup</c> stays on the pack.</summary>
    public static ToolBindings ForWorld(ToolCatalog catalog, string directory)
    {
        var map = new Dictionary<string, SourceBinding>(StringComparer.Ordinal);
        foreach (var tool in catalog.Tools)
        {
            if (tool.ImplementationFor("pack") is not null) map[tool.Name] = new SourceBinding(SourceBinding.PackId, "pack", new JsonObject());
            else if (tool.ImplementationFor("world") is not null)
                map[tool.Name] = new SourceBinding(SourceBinding.WorldId, "world", new JsonObject { ["directory"] = Path.GetFullPath(directory) });
        }
        return new ToolBindings(map);
    }
}

/// <summary>Per-incident executor state: call numbers, evidence ids, duplicates and budgets (operations.md 7.3).</summary>
public sealed class IncidentToolSession
{
    private readonly object _lock = new();
    private readonly Dictionary<string, int> _perTool = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _seen = new(StringComparer.Ordinal);
    private int _call;
    private int _evidence;   // E1 is the triggering signal
    private int _executed;
    private long _outputChars;

    /// <param name="incidentId">The incident the calls belong to.</param>
    /// <param name="lastEvidence">The last evidence number already used in the incident (1: only the triggering signal). The investigator starts after the
    /// evidence the rule engine stored, so its ids never collide with the report's.</param>
    public IncidentToolSession(string? incidentId = null, int lastEvidence = 1)
    {
        IncidentId = incidentId;
        _evidence = Math.Max(1, lastEvidence);
    }

    public string? IncidentId { get; }
    public int ExecutedCalls { get { lock (_lock) return _executed; } }
    public long OutputChars { get { lock (_lock) return _outputChars; } }

    public int NextCall() { lock (_lock) return ++_call; }
    public int CallsOf(string tool) { lock (_lock) return _perTool.GetValueOrDefault(tool); }
    public string? EarlierEvidence(string key) { lock (_lock) return _seen.GetValueOrDefault(key); }

    public string BeginExecution(string tool, string key)
    {
        lock (_lock)
        {
            _executed++;
            _perTool[tool] = _perTool.GetValueOrDefault(tool) + 1;
            var evidence = "E" + (++_evidence);
            _seen[key] = evidence;
            return evidence;
        }
    }

    /// <summary>Evidence ids are assigned in call order to every call, so a check always has one to cite.</summary>
    public string AllocateEvidence() { lock (_lock) return "E" + (++_evidence); }

    public void AddOutput(int chars) { lock (_lock) _outputChars += chars; }

    /// <summary>The state as JSON, so that one process per call (<c>tool --incident</c>) can continue an incident's numbering, duplicates and budgets.</summary>
    public JsonObject Snapshot()
    {
        lock (_lock)
        {
            var perTool = new JsonObject();
            foreach (var kv in _perTool.OrderBy(k => k.Key, StringComparer.Ordinal)) perTool[kv.Key] = kv.Value;
            var seen = new JsonObject();
            foreach (var kv in _seen.OrderBy(k => k.Key, StringComparer.Ordinal)) seen[kv.Key] = kv.Value;
            return new JsonObject { ["call"] = _call, ["evidence"] = _evidence, ["executed"] = _executed, ["output_chars"] = _outputChars, ["per_tool"] = perTool, ["seen"] = seen };
        }
    }

    public void Restore(JsonObject state)
    {
        lock (_lock)
        {
            _call = state["call"]?.GetValue<int>() ?? 0;
            _evidence = Math.Max(1, state["evidence"]?.GetValue<int>() ?? 1);
            _executed = state["executed"]?.GetValue<int>() ?? 0;
            _outputChars = state["output_chars"]?.GetValue<long>() ?? 0;
            _perTool.Clear();
            foreach (var kv in (state["per_tool"] as JsonObject) ?? []) _perTool[kv.Key] = kv.Value!.GetValue<int>();
            _seen.Clear();
            foreach (var kv in (state["seen"] as JsonObject) ?? []) _seen[kv.Key] = MonitorJson.ScalarText(kv.Value)!;
        }
    }
}

public sealed record ToolRequest(string Tool, JsonObject? Arguments, string Caller, IncidentToolSession Session);

/// <summary>The result envelope of every tool call (operations.md 7.1).</summary>
public sealed record ToolEnvelope(
    string Tool, int Call, string Status, string? EvidenceId, string? Source, int Rows, bool Truncated, int FieldsDropped, JsonNode? Data, string? Error)
{
    public bool IsSuccess => Status is ToolStatus.Ok or ToolStatus.Empty;

    public JsonObject ToJson() => new()
    {
        ["tool"] = Tool,
        ["call"] = Call,
        ["status"] = Status,
        ["evidence_id"] = EvidenceId,
        ["source"] = Source,
        ["rows"] = Rows,
        ["truncated"] = Truncated,
        ["fields_dropped"] = FieldsDropped,
        ["data"] = Data?.DeepClone(),
        ["error"] = Error,
    };
}

public sealed record ToolAuditRow(
    DateTimeOffset At, string? IncidentId, string Caller, string Tool, string ArgumentsJson, string? SourceId,
    string Status, int Rows, int OutputChars, long DurationMs, string? Error);

/// <summary>Receives one row per tool call, denied ones included. The store adds a <c>tool_audit</c> sink.</summary>
public interface IToolAuditSink
{
    Task WriteAsync(ToolAuditRow row, CancellationToken ct);
}

public sealed record EffectiveLimits(int MaxRows, int TimeoutSeconds, int MaxOutputChars, int? LookbackHours);

public sealed record ToolContext(ToolDefinition Tool, JsonObject Arguments, SourceBinding Source, JsonObject Settings, EffectiveLimits Limits);

public sealed record ToolResult(JsonNode Data, int Rows, bool Truncated = false);

/// <summary>One implementation per (tool family, source kind) (operations.md 14.1).</summary>
public interface IToolImplementation
{
    string Family { get; }
    string SourceKind { get; }
    Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct);
}

/// <summary>A failure reported to the caller as status <c>error</c>.</summary>
public sealed class ToolException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>An argument the schema cannot express (for example "exactly one of"): reported as status <c>denied</c>.</summary>
public sealed class ToolArgumentException(string message) : Exception(message);
