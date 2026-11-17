using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MyApp.Monitor.Tools;

public sealed record ToolLimits(int MaxRows, int TimeoutSeconds, int MaxOutputChars, int MaxCallsPerIncident, int? LookbackHours);

public sealed class ToolDefinition
{
    public string Name { get; init; } = "";
    public string Family { get; init; } = "";
    public string Access { get; init; } = "read";
    public string Description { get; init; } = "";
    public JsonObject Parameters { get; init; } = new();
    public ToolLimits Limits { get; init; } = new(1, 1, 1, 1, null);
    /// <summary>Source kind -> implementation settings (query text, file fields).</summary>
    public JsonObject Implementations { get; init; } = new();
    /// <summary><c>tools.{name}</c> or <c>entities</c>.</summary>
    public string MaskingSection { get; init; } = "";
    public JsonObject Raw { get; init; } = new();

    public JsonObject? ImplementationFor(string sourceKind) => Implementations[sourceKind] as JsonObject;
}

/// <summary>The tool catalog (operations.md 7.1, <c>docs/support/tools.json</c>).</summary>
public sealed class ToolCatalog
{
    public static readonly string[] Families = ["logs", "entity", "audit", "health", "signatures"];
    private static readonly Regex NameRegex = new(@"^[a-z]+(_[a-z]+)*$", RegexOptions.CultureInvariant);

    private readonly Dictionary<string, ToolDefinition> _byName;

    public ToolCatalog(IEnumerable<ToolDefinition> tools)
    {
        Tools = tools.ToList();
        _byName = Tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<ToolDefinition> Tools { get; }
    public ToolDefinition? Find(string name) => _byName.GetValueOrDefault(name);
    public IReadOnlySet<string> Names => _byName.Keys.ToHashSet(StringComparer.Ordinal);

    public static ToolCatalog Load(string json)
    {
        var findings = new FindingList();
        var catalog = Parse(MonitorJson.Parse(json), findings);
        if (findings.Count > 0) throw new MonitorException("tools.json is invalid: " + string.Join("; ", findings.Select(f => f.Message)));
        return catalog;
    }

    public static ToolCatalog Parse(JsonNode? root, FindingList findings)
    {
        const string file = "tools.json";
        var tools = new List<ToolDefinition>();
        if (root is not JsonObject obj) { findings.Add("O08", "must be a JSON object", file); return new ToolCatalog(tools); }
        if (MonitorJson.ScalarText(obj["schema_version"]) != "1") findings.Add("O08", "schema_version must be 1", file);
        if (obj["tools"] is not JsonArray arr) { findings.Add("O08", "tools must be an array", file); return new ToolCatalog(tools); }

        string? previous = null;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in arr)
        {
            if (node is not JsonObject t) { findings.Add("O08", "every tool must be an object", file); continue; }
            var name = MonitorJson.ScalarText(t["name"]) ?? "";
            var where = $"{file}: {name}";
            var ok = true;
            void Fail(string m) { findings.Add("O08", m, where); ok = false; }

            if (!NameRegex.IsMatch(name)) Fail($"name '{name}' must match ^[a-z]+(_[a-z]+)*$");
            if (!names.Add(name)) Fail("name is duplicated");
            if (previous is not null && string.CompareOrdinal(previous, name) > 0) Fail("tools must be ordered by name");
            previous = name;

            var family = MonitorJson.ScalarText(t["family"]) ?? "";
            if (!Families.Contains(family)) Fail($"family '{family}' is not one of {string.Join(", ", Families)}");
            var access = MonitorJson.ScalarText(t["access"]) ?? "";
            if (access != "read") Fail("access must be 'read'");
            var description = MonitorJson.ScalarText(t["description"]) ?? "";
            if (description.Length == 0) Fail("description is empty");

            var parameters = t["parameters"] as JsonObject;
            if (parameters is null) Fail("parameters must be an object");
            else
            {
                if (MonitorJson.ScalarText(parameters["type"]) != "object") Fail("parameters.type must be 'object'");
                if (parameters["additionalProperties"] is not JsonValue ap || !ap.TryGetValue<bool>(out var apv) || apv)
                    Fail("parameters.additionalProperties must be false");
                foreach (var p in SchemaSubsetValidator.CheckSubset(parameters, "parameters")) Fail(p);
            }

            var limits = ParseLimits(t["limits"] as JsonObject, Fail);
            var impls = t["implementations"] as JsonObject;
            if (impls is null || impls.Count == 0) Fail("implementations must list at least one source kind");

            var masking = MonitorJson.ScalarText((t["output"] as JsonObject)?["masking"]) ?? "";
            if (masking != "entities" && masking != $"tools.{name}") Fail($"output.masking must be 'tools.{name}' or 'entities'");

            if (ok)
                tools.Add(new ToolDefinition
                {
                    Name = name, Family = family, Access = access, Description = description, Parameters = (JsonObject)parameters!.DeepClone(),
                    Limits = limits!, Implementations = (JsonObject)impls!.DeepClone(), MaskingSection = masking, Raw = (JsonObject)t.DeepClone(),
                });
        }
        return new ToolCatalog(tools);
    }

    private static ToolLimits? ParseLimits(JsonObject? limits, Action<string> fail)
    {
        if (limits is null) { fail("limits must be an object"); return null; }
        int Need(string name)
        {
            if (limits[name] is JsonValue v && v.TryGetValue<int>(out var i) && i > 0) return i;
            fail($"limits.{name} must be a positive integer");
            return 1;
        }
        var result = new ToolLimits(Need("max_rows"), Need("timeout_seconds"), Need("max_output_chars"), Need("max_calls_per_incident"), null);
        if (limits["lookback_hours"] is { } lb)
        {
            if (lb is JsonValue lv && lv.TryGetValue<int>(out var hours) && hours > 0) result = result with { LookbackHours = hours };
            else fail("limits.lookback_hours must be a positive integer");
        }
        foreach (var kv in limits)
            if (kv.Key is not ("max_rows" or "timeout_seconds" or "max_output_chars" or "max_calls_per_incident" or "lookback_hours"))
                fail($"limits.{kv.Key} is not a known limit");
        return result;
    }

    /// <summary>The catalog reduced to the named tools (pack <c>tools.json</c>, operations.md 13.1).</summary>
    public static JsonNode Reduce(JsonNode catalogJson, IReadOnlySet<string> keep)
    {
        var copy = (JsonObject)catalogJson.DeepClone();
        var arr = new JsonArray();
        foreach (var t in (copy["tools"] as JsonArray) ?? [])
            if (MonitorJson.ScalarText(t?["name"]) is { } name && keep.Contains(name)) arr.Add(t!.DeepClone());
        copy["tools"] = arr;
        return copy;
    }
}

public sealed record EntityDefinition(string Name, string Description, string SourceKind, string Table, string Key, string KeyType, string? AuditEntityType);

/// <summary>Entity binding (operations.md 7.2, <c>docs/support/entities.json</c>).</summary>
public sealed class EntityCatalog
{
    private static readonly Regex NameRegex = new(@"^[a-z]+(_[a-z]+)*$", RegexOptions.CultureInvariant);
    public static readonly Regex Identifier = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
    private static readonly string[] KeyTypes = ["guid", "int", "string"];

    private readonly Dictionary<string, EntityDefinition> _byName;

    public EntityCatalog(IEnumerable<EntityDefinition> entities)
    {
        Entities = entities.ToList();
        _byName = Entities.ToDictionary(e => e.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<EntityDefinition> Entities { get; }
    public IReadOnlyCollection<string> Names => _byName.Keys;
    public EntityDefinition? Find(string name) => _byName.GetValueOrDefault(name);

    public static EntityCatalog Load(string json)
    {
        var findings = new FindingList();
        var catalog = Parse(MonitorJson.Parse(json), findings);
        if (findings.Count > 0) throw new MonitorException("entities.json is invalid: " + string.Join("; ", findings.Select(f => f.Message)));
        return catalog;
    }

    public static EntityCatalog Parse(JsonNode? root, FindingList findings)
    {
        const string file = "entities.json";
        var list = new List<EntityDefinition>();
        if (root is not JsonObject obj) { findings.Add("O08", "must be a JSON object", file); return new EntityCatalog(list); }
        if (MonitorJson.ScalarText(obj["schema_version"]) != "1") findings.Add("O08", "schema_version must be 1", file);
        if (obj["entities"] is not JsonArray arr) { findings.Add("O08", "entities must be an array", file); return new EntityCatalog(list); }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in arr)
        {
            if (node is not JsonObject e) { findings.Add("O08", "every entity must be an object", file); continue; }
            var name = MonitorJson.ScalarText(e["name"]) ?? "";
            var where = $"{file}: {name}";
            var ok = true;
            void Fail(string m) { findings.Add("O08", m, where); ok = false; }
            if (!NameRegex.IsMatch(name)) Fail($"name '{name}' must match ^[a-z]+(_[a-z]+)*$");
            if (!names.Add(name)) Fail("name is duplicated");
            var description = MonitorJson.ScalarText(e["description"]) ?? "";
            if (description.Length == 0) Fail("description is empty");
            var kind = MonitorJson.ScalarText(e["source_kind"]) ?? "";
            if (kind.Length == 0) Fail("source_kind is missing");
            var table = MonitorJson.ScalarText(e["table"]) ?? "";
            var parts = table.Split('.');
            if (parts.Length != 2 || !parts.All(p => Identifier.IsMatch(p))) Fail($"table '{table}' must be schema.table with plain identifiers");
            var key = MonitorJson.ScalarText(e["key"]) ?? "";
            if (!Identifier.IsMatch(key)) Fail($"key '{key}' is not a plain identifier");
            var keyType = MonitorJson.ScalarText(e["key_type"]) ?? "";
            if (!KeyTypes.Contains(keyType)) Fail($"key_type must be one of {string.Join(", ", KeyTypes)}");
            var audit = e["audit_entity_type"] is null ? null : MonitorJson.ScalarText(e["audit_entity_type"]);
            if (!e.ContainsKey("audit_entity_type")) Fail("audit_entity_type is missing (use null)");
            if (ok) list.Add(new EntityDefinition(name, description, kind, table, key, keyType, audit));
        }
        return new EntityCatalog(list);
    }
}

/// <summary>Constraints (operations.md section 9, <c>docs/support/constraints.json</c>).</summary>
public sealed class ConstraintSet
{
    public static readonly string[] Types = ["access", "budget", "scope", "data", "action"];
    public static readonly string[] Enforcers = ["pack_verify", "executor", "investigator_host", "notifier", "store"];
    private static readonly Regex IdRegex = new(@"^C\d{2}$", RegexOptions.CultureInvariant);

    public sealed record Constraint(string Id, string Type, string Text, JsonObject Params, IReadOnlyList<string> EnforcedBy);

    public ConstraintSet(IEnumerable<Constraint> constraints) => Constraints = constraints.ToList();

    public IReadOnlyList<Constraint> Constraints { get; }

    public IReadOnlyList<string> AllowedAccess =>
        ((Find("C01")?.Params["allowed_access"] as JsonArray)?.Select(n => MonitorJson.ScalarText(n) ?? "").ToList()) ?? ["read"];
    public int? MaxToolCalls => IntParam("C03", "max_tool_calls");
    public int? MaxOutputChars => IntParam("C03", "max_output_chars");
    public int? MaxLookbackHours => IntParam("C04", "max_lookback_hours");
    public string ApplicationIntent => MonitorJson.ScalarText(Find("C02")?.Params["application_intent"]) ?? "ReadOnly";

    public Constraint? Find(string id) => Constraints.FirstOrDefault(c => c.Id == id);

    private int? IntParam(string id, string name) => Find(id)?.Params[name] is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;

    public static ConstraintSet Load(string json)
    {
        var findings = new FindingList();
        var set = Parse(MonitorJson.Parse(json), findings);
        if (findings.Count > 0) throw new MonitorException("constraints.json is invalid: " + string.Join("; ", findings.Select(f => f.Message)));
        return set;
    }

    public static ConstraintSet Parse(JsonNode? root, FindingList findings)
    {
        const string file = "constraints.json";
        var list = new List<Constraint>();
        if (root is not JsonObject obj) { findings.Add("O08", "must be a JSON object", file); return new ConstraintSet(list); }
        if (MonitorJson.ScalarText(obj["schema_version"]) != "1") findings.Add("O08", "schema_version must be 1", file);
        if (obj["constraints"] is not JsonArray arr) { findings.Add("O08", "constraints must be an array", file); return new ConstraintSet(list); }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in arr)
        {
            if (node is not JsonObject c) { findings.Add("O08", "every constraint must be an object", file); continue; }
            var id = MonitorJson.ScalarText(c["id"]) ?? "";
            var where = $"{file}: {id}";
            var ok = true;
            void Fail(string m) { findings.Add("O08", m, where); ok = false; }
            if (!IdRegex.IsMatch(id)) Fail($"id '{id}' must be C and two digits");
            if (!ids.Add(id)) Fail("id is duplicated");
            var type = MonitorJson.ScalarText(c["type"]) ?? "";
            if (!Types.Contains(type)) Fail($"type '{type}' is not one of {string.Join(", ", Types)}");
            var text = MonitorJson.ScalarText(c["text"]) ?? "";
            if (text.Length == 0) Fail("text is empty");
            if (c["params"] is not JsonObject p) { Fail("params must be an object"); p = new JsonObject(); }
            var enforced = (c["enforced_by"] as JsonArray)?.Select(n => MonitorJson.ScalarText(n) ?? "").ToList() ?? [];
            if (enforced.Count == 0) Fail("enforced_by must name at least one component");
            foreach (var e in enforced.Where(e => !Enforcers.Contains(e))) Fail($"enforced_by '{e}' is not one of {string.Join(", ", Enforcers)}");
            if (ok) list.Add(new Constraint(id, type, text, (JsonObject)p.DeepClone(), enforced));
        }
        return new ConstraintSet(list);
    }
}
