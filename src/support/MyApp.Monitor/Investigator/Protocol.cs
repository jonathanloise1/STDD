using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Investigator;

/// <summary>An assistant turn: exactly one call, one report, or something that is neither (<see cref="Kind"/> <c>invalid</c>).</summary>
public sealed record Turn(string Kind, JsonObject? Payload, string? Error)
{
    public const string Call = "call";
    public const string Report = "report";
    public const string Invalid = "invalid";
}

/// <summary>
/// The investigator protocol of operations.md 11.3 in C#: turn formats and stop-rule numbers from <c>training/investigator/protocol.json</c>
/// (embedded; the single definition), rendering, parsing and report validation. The golden file next to that definition pins the bytes.
/// </summary>
public sealed class Protocol
{
    public const string ResourceName = "investigator-protocol.json";
    /// <summary>The <c>tool_role</c> when the lock does not record one: results travel with the protocol's own result role.</summary>
    public const string ProtocolToolRole = "tool";
    private static readonly Regex Placeholder = new(@"\{(\w+)\}", RegexOptions.CultureInvariant);
    private static readonly Regex CallRx = new("^<call>(.*)</call>$", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex ReportRx = new("^<report>(.*)</report>$", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public static readonly string[] CauseCategories =
        ["access", "data_missing", "data_conflict", "dependency_unavailable", "configuration", "capacity", "code_defect", "external_input", "expected_behaviour", "unknown"];
    public static readonly string[] FailedStatuses = [ToolStatus.Error, ToolStatus.Denied, ToolStatus.Timeout];

    private readonly JsonObject _turns;

    private Protocol(JsonObject root)
    {
        Root = root;
        _turns = (JsonObject)root["turns"]!;
        ResultRole = MonitorJson.ScalarText(root["result_role"])!;
        var budget = (JsonObject)root["budget"]!;
        BudgetSmall = budget["small"]!.GetValue<int>();
        BudgetLarge = budget["large"]!.GetValue<int>();
        BudgetCap = budget["cap"]!.GetValue<int>();
        var stop = (JsonObject)root["stop_rules"]!;
        DuplicateLimit = stop["duplicate_limit"]!.GetValue<int>();
        ConsecutiveFailures = stop["consecutive_failures"]!.GetValue<int>();
        SummaryFraction = stop["context_summary_fraction"]!.GetValue<double>();
        var report = (JsonObject)root["report"]!;
        ReportDef = MonitorJson.ScalarText(report["def"])!;
        RepairTurns = report["repair_turns"]!.GetValue<int>();
        MinConfidenceExplained = report["min_confidence_explained"]!.GetValue<double>();
        MaxNewTokens = ((JsonObject)root["decoding"]!)["max_new_tokens"]!.GetValue<int>();
    }

    public JsonObject Root { get; }
    public string ResultRole { get; }
    public int BudgetSmall { get; }
    public int BudgetLarge { get; }
    public int BudgetCap { get; }
    public int DuplicateLimit { get; }
    public int ConsecutiveFailures { get; }
    public double SummaryFraction { get; }
    public string ReportDef { get; }
    public int RepairTurns { get; }
    public double MinConfidenceExplained { get; }
    public int MaxNewTokens { get; }

    /// <summary>
    /// The role a message is sent with: the result role becomes <c>user</c> with <c>tool_role: user</c> (the chat template of Qwen3.5 and later renders a
    /// <c>tool</c> message in its native tool format), everything else is unchanged. The same mapping as <c>RoleMappedBackend</c> in the training pipeline.
    /// </summary>
    public string WireRole(string role, string? toolRole) => toolRole == "user" && role == ResultRole ? "user" : role;

    public int DefaultBudget(string tier) => tier == "large" ? BudgetLarge : BudgetSmall;

    public static Protocol Load()
    {
        using var stream = typeof(Protocol).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new MonitorException("the investigator protocol definition is not embedded in this build");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    public static Protocol Parse(string json) => new((JsonObject)MonitorJson.Parse(json)!);

    // ---- rendering ----

    /// <summary>One left-to-right pass: substituted values are not scanned again (braces inside values stay literal).</summary>
    private static string Fill(string template, IReadOnlyDictionary<string, string> values) =>
        Placeholder.Replace(template, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

    private string Template(string name) => MonitorJson.ScalarText(_turns[name])!;

    /// <summary>The first user turn. <paramref name="tools"/> hold name, description and parameters; <paramref name="entities"/> name and description.</summary>
    public string RenderUser(
        JsonNode? incident, JsonNode? pack, IEnumerable<(string Name, string Description, JsonNode Parameters)> tools,
        IEnumerable<(string Name, string Description)> entities, IEnumerable<(string Id, string Text)> constraints, int calls)
    {
        var toolJson = new JsonObject
        {
            ["tools"] = new JsonArray(tools.Select(t => (JsonNode?)new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["parameters"] = t.Parameters.DeepClone() }).ToArray()),
            ["entities"] = new JsonArray(entities.Select(e => (JsonNode?)new JsonObject { ["name"] = e.Name, ["description"] = e.Description }).ToArray()),
        };
        var line = Template("constraint_line");
        var lines = string.Join("\n", constraints.Select(c => line.Replace("{id}", c.Id, StringComparison.Ordinal).Replace("{text}", c.Text, StringComparison.Ordinal)));
        return Fill(Template("user"), new Dictionary<string, string>
        {
            ["incident"] = ProtocolJson.Dumps(incident), ["pack"] = ProtocolJson.Dumps(pack), ["tools"] = ProtocolJson.Dumps(toolJson),
            ["constraints"] = lines, ["calls"] = calls.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
    }

    public string RenderCall(string tool, JsonNode? arguments) =>
        Fill(Template("call"), new Dictionary<string, string> { ["call"] = ProtocolJson.Dumps(new JsonObject { ["tool"] = tool, ["arguments"] = arguments?.DeepClone() ?? new JsonObject() }) });

    public string RenderReport(JsonNode report) => Fill(Template("report"), new Dictionary<string, string> { ["report"] = ProtocolJson.Dumps(report) });

    public string RenderResult(JsonNode envelope, int callsLeft) =>
        Fill(Template("result"), new Dictionary<string, string>
        {
            ["result"] = ProtocolJson.Dumps(envelope), ["calls"] = Math.Max(0, callsLeft).ToString(System.Globalization.CultureInfo.InvariantCulture),
        });

    /// <summary>The user turn of the one repair allowed after an invalid report (11.3).</summary>
    public string RenderRepair(IEnumerable<string> errors)
    {
        var joined = string.Join("; ", errors);
        return Fill(Template("repair"), new Dictionary<string, string> { ["errors"] = joined.Length <= 600 ? joined : joined[..600] });
    }

    // ---- parsing ----

    /// <summary>An assistant turn is exactly one <c>&lt;call&gt;{json}&lt;/call&gt;</c> or <c>&lt;report&gt;{json}&lt;/report&gt;</c> (surrounding whitespace ignored).</summary>
    public static Turn ParseTurn(string text)
    {
        var t = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        foreach (var (rx, kind) in new[] { (CallRx, Turn.Call), (ReportRx, Turn.Report) })
        {
            var m = rx.Match(t);
            if (!m.Success) continue;
            JsonNode? data;
            try { data = JsonNode.Parse(m.Groups[1].Value); }
            catch (System.Text.Json.JsonException ex) { return new Turn(Turn.Invalid, null, $"{kind} is not JSON: {ex.Message}"); }
            if (data is not JsonObject obj) return new Turn(Turn.Invalid, null, $"{kind} is not an object");
            if (kind == Turn.Call
                && (obj.Count != 2 || !obj.ContainsKey("tool") || !obj.ContainsKey("arguments") || obj["tool"] is not JsonValue tv || !tv.TryGetValue<string>(out _) || obj["arguments"] is not JsonObject))
                return new Turn(Turn.Invalid, null, "a call has exactly \"tool\" and \"arguments\"");
            return new Turn(kind, obj, null);
        }
        return new Turn(Turn.Invalid, null, "neither a call nor a report");
    }

    // ---- validation ----

    /// <summary>The <c>investigation</c> definition of the incident report schema with its <c>$defs</c> (what <c>$ref</c>s inside it resolve against).</summary>
    public JsonObject InvestigationSchema(JsonNode incidentReportSchema)
    {
        var defs = (JsonObject)incidentReportSchema["$defs"]!;
        var schema = (JsonObject)defs[ReportDef[(ReportDef.LastIndexOf('/') + 1)..]]!.DeepClone();
        schema["$defs"] = defs.DeepClone();
        return schema;
    }

    /// <summary>
    /// Validation after the report (11.3): schema, every <c>evidence_ref</c> was received, <c>user_story</c> is in the pack or null, and the
    /// field rules of the table. <paramref name="stories"/> is null when there is no pack.
    /// </summary>
    public static List<string> ValidateReport(SchemaSubsetValidator validator, JsonNode? report, IReadOnlySet<string> evidenceIds, IReadOnlySet<string>? stories)
    {
        var errors = validator.Validate(report);
        if (errors.Count > 0 || report is not JsonObject r) return errors;
        foreach (var refNode in (JsonArray)r["evidence_refs"]!)
        {
            var id = MonitorJson.ScalarText(refNode)!;
            if (!evidenceIds.Contains(id)) errors.Add($"evidence_refs: {id} was not received");
        }
        var story = MonitorJson.ScalarText(r["user_story"]);
        if (story is not null && (stories is null || !stories.Contains(story))) errors.Add($"user_story {story} is not in the pack");
        var explained = MonitorJson.ScalarText(r["verdict"]) == "explained";
        if (explained)
        {
            if (r["abstain_reason"] is not null) errors.Add("abstain_reason must be null when explained");
            if (((JsonArray)r["evidence_refs"]!).Count == 0) errors.Add("an explained report cites at least one evidence id");
            if (r["cause"] is null) errors.Add("an explained report has a cause");
        }
        else
        {
            if (r["abstain_reason"] is null) errors.Add("an abstain report has an abstain_reason");
            if (r["cause"] is not null) errors.Add("cause must be null when abstaining");
        }
        return errors;
    }

    /// <summary><c>confidence</c> below the minimum with <c>explained</c> is rewritten to <c>abstain</c> (<c>insufficient_evidence</c>).</summary>
    public JsonObject NormaliseReport(JsonObject report)
    {
        var copy = (JsonObject)report.DeepClone();
        if (MonitorJson.ScalarText(copy["verdict"]) == "explained" && copy["confidence"]!.GetValue<double>() < MinConfidenceExplained)
        {
            copy["verdict"] = "abstain";
            copy["abstain_reason"] = "insufficient_evidence";
        }
        return copy;
    }
}
