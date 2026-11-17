using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using MyApp.Monitor.Tools;
using YamlDotNet.RepresentationModel;

namespace MyApp.Monitor.Pack;

public sealed class SourceSettings
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    /// <summary>Every key of the source entry, placeholders resolved.</summary>
    public JsonObject Settings { get; init; } = new();

    public string? GetString(string name) => MonitorJson.ScalarText(Settings[name]);

    public int GetInt(string name, int fallback) =>
        int.TryParse(GetString(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public IReadOnlyList<string> GetStrings(string name) => Settings[name] switch
    {
        JsonArray a => a.Select(n => MonitorJson.ScalarText(n) ?? "").Where(s => s.Length > 0).ToList(),
        JsonValue v when MonitorJson.ScalarText(v) is { Length: > 0 } s => [s],
        _ => [],
    };
}

public sealed class AnalyserSettings
{
    public int WindowMinutes { get; init; } = 60;
    public int RateMultiplier { get; init; } = 3;
    public int RateMinCount { get; init; } = 5;
    public int UnknownWarningMinCount { get; init; } = 3;
    public string AppNamespacePrefix { get; init; } = "MyApp.";
}

public sealed class InvestigatorSettings
{
    public string Tier { get; init; } = "small";
    public string Device { get; init; } = "auto";
    public int? MaxToolCalls { get; init; }
    public int TimeoutSeconds { get; init; } = 600;
    public int Concurrency { get; init; } = 1;
}

public sealed class GateSettings
{
    public string TicketMinSeverity { get; init; } = "Error";
    public bool TicketAllowModelVerdict { get; init; }
    public string EmailMinSeverity { get; init; } = "Error";
    public string EmailDigestTime { get; init; } = "08:00";
}

public sealed class StoreSettings
{
    public string Kind { get; init; } = "sqlite";
    public string Path { get; init; } = "data/monitor.db";
    public int RetentionDays { get; init; } = 90;
}

public sealed class DashboardSettings
{
    public bool Enabled { get; init; } = true;
    public string? Urls { get; init; }
    public string Auth { get; init; } = "local";
    public string? AllowedGroupId { get; init; }
}

/// <summary>A profile (operations.md 12.1) with placeholders resolved. Sections of later passes stay as JSON.</summary>
public sealed class Profile
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Environment { get; init; } = "";
    public IReadOnlyList<SourceSettings> Sources { get; init; } = [];
    public IReadOnlyDictionary<string, string> ToolSources { get; init; } = new Dictionary<string, string>();
    public AnalyserSettings Analyser { get; init; } = new();
    public InvestigatorSettings Investigator { get; init; } = new();
    public GateSettings Gates { get; init; } = new();
    public JsonNode? Notifications { get; init; }
    public JsonNode? Suppression { get; init; }
    public StoreSettings Store { get; init; } = new();
    public DashboardSettings Dashboard { get; init; } = new();
    public bool EvidenceInNotifications { get; init; } = true;
    /// <summary>The resolved document, for components added later.</summary>
    public JsonObject Raw { get; init; } = new();
    /// <summary>Placeholders without a value and without a default.</summary>
    public IReadOnlyList<string> UnresolvedSettings { get; init; } = [];

    public SourceSettings? FindSource(string id) => Sources.FirstOrDefault(s => s.Id == id);
}

public static class ProfileLoader
{
    public static readonly string[] SignalKinds = ["log_analytics", "log_files", "http_health", "world"];
    public static readonly string[] ToolOnlyKinds = ["sqlserver"];
    public const string PackSourceId = "pack";

    private static readonly Regex Placeholder = new(@"\$\{setting:(?<key>[^}|]+)(?:\|(?<default>[^}]*))?\}", RegexOptions.CultureInvariant);
    private static readonly string[] SecretKeyWords = ["password", "secret", "key", "token", "connection"];

    // ---- YAML -> JSON ----

    public static JsonObject ParseYaml(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
            throw new MonitorException("profile must be a YAML mapping");
        return (JsonObject)Convert(root)!;
    }

    private static JsonNode? Convert(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode map:
            {
                var obj = new JsonObject();
                foreach (var kv in map.Children) obj[((YamlScalarNode)kv.Key).Value ?? ""] = Convert(kv.Value);
                return obj;
            }
            case YamlSequenceNode seq:
            {
                var arr = new JsonArray();
                foreach (var item in seq.Children) arr.Add(Convert(item));
                return arr;
            }
            case YamlScalarNode scalar:
            {
                var text = scalar.Value ?? "";
                if (scalar.Style != YamlDotNet.Core.ScalarStyle.Plain) return JsonValue.Create(text);
                if (text.Length == 0 || text == "~" || text == "null") return null;
                if (text == "true") return JsonValue.Create(true);
                if (text == "false") return JsonValue.Create(false);
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return JsonValue.Create(l);
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && text.Contains('.')) return JsonValue.Create(d);
                return JsonValue.Create(text);
            }
            default:
                return null;
        }
    }

    // ---- placeholders ----

    public static JsonNode? Resolve(JsonNode? node, Func<string, string?> setting, List<string> unresolved)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var copy = new JsonObject();
                foreach (var kv in obj) copy[kv.Key] = Resolve(kv.Value, setting, unresolved);
                return copy;
            }
            case JsonArray arr:
            {
                var copy = new JsonArray();
                foreach (var item in arr) copy.Add(Resolve(item, setting, unresolved));
                return copy;
            }
            case JsonValue v when v.TryGetValue<string>(out var s) && s.Contains("${setting:", StringComparison.Ordinal):
            {
                var replaced = Placeholder.Replace(s, m =>
                {
                    var key = m.Groups["key"].Value;
                    var value = setting(key);
                    if (!string.IsNullOrEmpty(value)) return value;
                    if (m.Groups["default"].Success) return m.Groups["default"].Value;
                    unresolved.Add(key);
                    return "";
                });
                return JsonValue.Create(replaced);
            }
            default:
                return node?.DeepClone();
        }
    }

    public static Profile Load(string yaml, IConfiguration? settings, string? expectedName = null)
    {
        var raw = ParseYaml(yaml);
        var findings = new FindingList();
        Validate(raw, expectedName, findings, null);
        if (findings.Count > 0) throw new MonitorException("profile is invalid: " + string.Join("; ", findings.Select(f => f.Message)));
        return Bind(raw, settings);
    }

    public static Profile Bind(JsonObject raw, IConfiguration? settings)
    {
        var unresolved = new List<string>();
        var resolved = (JsonObject)Resolve(raw, key => settings?[key], unresolved)!;

        string Str(JsonNode? n, string fallback = "") => MonitorJson.ScalarText(n) ?? fallback;
        int Int(JsonNode? n, int fallback) => int.TryParse(MonitorJson.ScalarText(n), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        bool Bool(JsonNode? n, bool fallback) => bool.TryParse(MonitorJson.ScalarText(n), out var v) ? v : fallback;

        var sources = new List<SourceSettings>();
        foreach (var s in (resolved["sources"] as JsonArray) ?? [])
            if (s is JsonObject so)
                sources.Add(new SourceSettings { Id = Str(so["id"]), Kind = Str(so["kind"]), Settings = (JsonObject)so.DeepClone() });

        var toolSources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in (resolved["tool_sources"] as JsonObject) ?? [])
            toolSources[kv.Key] = Str(kv.Value);

        var an = resolved["analyser"] as JsonObject;
        var inv = resolved["investigator"] as JsonObject;
        var gates = resolved["gates"] as JsonObject;
        var store = resolved["store"] as JsonObject;
        var dash = resolved["dashboard"] as JsonObject;
        var perimeter = resolved["perimeter"] as JsonObject;

        return new Profile
        {
            Name = Str(resolved["name"]),
            Description = Str(resolved["description"]),
            Environment = Str(resolved["environment"], "prod"),
            Sources = sources,
            ToolSources = toolSources,
            Analyser = new AnalyserSettings
            {
                WindowMinutes = Int(an?["window_minutes"], 60),
                RateMultiplier = Int(an?["rate_multiplier"], 3),
                RateMinCount = Int(an?["rate_min_count"], 5),
                UnknownWarningMinCount = Int(an?["unknown_warning_min_count"], 3),
                AppNamespacePrefix = Str(an?["app_namespace_prefix"], "MyApp."),
            },
            Investigator = new InvestigatorSettings
            {
                Tier = Str(inv?["tier"], "small"),
                Device = Str(inv?["device"], "auto"),
                MaxToolCalls = inv?["max_tool_calls"] is null ? null : Int(inv["max_tool_calls"], 6),
                TimeoutSeconds = Int(inv?["timeout_seconds"], 600),
                Concurrency = Int(inv?["concurrency"], 1),
            },
            Gates = new GateSettings
            {
                TicketMinSeverity = Str(gates?["ticket"]?["min_severity"], "Error"),
                TicketAllowModelVerdict = Bool(gates?["ticket"]?["allow_model_verdict"], false),
                EmailMinSeverity = Str(gates?["email"]?["min_severity"], "Error"),
                EmailDigestTime = Str(gates?["email"]?["digest_time"], "08:00"),
            },
            Notifications = resolved["notifications"]?.DeepClone(),
            Suppression = resolved["suppression"]?.DeepClone(),
            Store = new StoreSettings { Kind = Str(store?["kind"], "sqlite"), Path = Str(store?["path"], "data/monitor.db"), RetentionDays = Int(store?["retention_days"], 90) },
            Dashboard = new DashboardSettings
            {
                Enabled = Bool(dash?["enabled"], true),
                Urls = dash?["urls"] is null ? null : Str(dash["urls"]),
                Auth = Str(dash?["auth"], "local"),
                AllowedGroupId = dash?["allowed_group_id"] is null ? null : Str(dash["allowed_group_id"]),
            },
            EvidenceInNotifications = Bool(perimeter?["evidence_in_notifications"], true),
            Raw = resolved,
            UnresolvedSettings = unresolved.Distinct().ToList(),
        };
    }

    // ---- validation (12.1, check O08) ----

    /// <summary>Validates the raw (unresolved) profile. Pass the catalog to check tool bindings.</summary>
    public static void Validate(JsonObject raw, string? expectedName, FindingList findings, ToolCatalog? catalog, string location = "profile")
    {
        void Add(string m) => findings.Add("O08", m, location);

        if (MonitorJson.ScalarText(raw["schema_version"]) != "1") Add("schema_version must be 1");
        var name = MonitorJson.ScalarText(raw["name"]) ?? "";
        if (name.Length == 0) Add("name is missing");
        else if (expectedName is not null && name != expectedName) Add($"name '{name}' must equal the profile name '{expectedName}'");
        if (string.IsNullOrWhiteSpace(MonitorJson.ScalarText(raw["description"]))) Add("description is missing");
        if (string.IsNullOrWhiteSpace(MonitorJson.ScalarText(raw["environment"]))) Add("environment is missing");

        var sourceKinds = new Dictionary<string, string>(StringComparer.Ordinal) { [PackSourceId] = "pack" };
        if (raw["sources"] is not JsonArray sources || sources.Count == 0) Add("sources must list at least one source");
        else
        {
            foreach (var node in sources)
            {
                if (node is not JsonObject s) { Add("every source must be a mapping"); continue; }
                var id = MonitorJson.ScalarText(s["id"]) ?? "";
                var kind = MonitorJson.ScalarText(s["kind"]) ?? "";
                if (id.Length == 0) { Add("a source has no id"); continue; }
                if (id == PackSourceId) Add("source id 'pack' is reserved (the pack source is implicit)");
                if (!sourceKinds.TryAdd(id, kind)) Add($"source id '{id}' is duplicated");
                if (!SignalKinds.Contains(kind) && !ToolOnlyKinds.Contains(kind)) Add($"source '{id}': kind '{kind}' is not one of {string.Join(", ", SignalKinds.Concat(ToolOnlyKinds))}");
                if (kind == "world") Add($"source '{id}': a world source is for tests and training only and never belongs to a profile (O08)");
                ValidateSource(id, kind, s, Add);
            }
        }

        if (raw["tool_sources"] is not JsonObject toolSources || toolSources.Count == 0) Add("tool_sources must bind at least one tool");
        else
        {
            foreach (var kv in toolSources)
            {
                var sourceId = MonitorJson.ScalarText(kv.Value) ?? "";
                if (!sourceKinds.TryGetValue(sourceId, out var kind)) { Add($"tool_sources.{kv.Key}: source '{sourceId}' is not defined in sources"); continue; }
                if (kv.Key == "signature_lookup" && sourceId != PackSourceId) Add("tool_sources.signature_lookup must be 'pack'");
                if (catalog is null) continue;
                var tool = catalog.Find(kv.Key);
                if (tool is null) Add($"tool_sources.{kv.Key}: no such tool in the catalog");
                else if (tool.ImplementationFor(kind) is null) Add($"tool_sources.{kv.Key}: tool has no implementation for source kind '{kind}' (source '{sourceId}')");
            }
        }

        if (raw["analyser"] is not JsonObject an) Add("analyser section is missing");
        else
            foreach (var key in new[] { "window_minutes", "rate_multiplier", "rate_min_count", "unknown_warning_min_count", "app_namespace_prefix" })
                if (an[key] is null) Add($"analyser.{key} is missing");

        if (raw["investigator"] is not JsonObject inv) Add("investigator section is missing");
        else
        {
            var tier = MonitorJson.ScalarText(inv["tier"]);
            if (tier is not null && !tier.Contains("${") && tier is not ("none" or "small" or "large")) Add("investigator.tier must be none, small or large");
            var device = MonitorJson.ScalarText(inv["device"]);
            if (device is not null && !device.Contains("${") && device is not ("auto" or "gpu" or "cpu")) Add("investigator.device must be auto, gpu or cpu");
        }

        if (raw["gates"] is not JsonObject gates) Add("gates section is missing");
        else if (MonitorJson.ScalarText(gates["ticket"]?["allow_model_verdict"]) is { } allow && allow != "false")
            Add("gates.ticket.allow_model_verdict must stay false in version 1");

        if (raw["notifications"] is not JsonObject) Add("notifications section is missing");
        if (raw["suppression"] is not JsonArray) Add("suppression must be a list (use [])");
        if (raw["store"] is not JsonObject store) Add("store section is missing");
        else
        {
            if (MonitorJson.ScalarText(store["kind"]) != "sqlite") Add("store.kind must be sqlite");
            if (string.IsNullOrWhiteSpace(MonitorJson.ScalarText(store["path"]))) Add("store.path is missing");
        }
        if (raw["dashboard"] is not JsonObject dash) Add("dashboard section is missing");
        else
        {
            var auth = MonitorJson.ScalarText(dash["auth"]);
            if (auth is not null && !auth.Contains("${") && auth is not ("local" or "entra")) Add("dashboard.auth must be local or entra");
        }
        if (raw["perimeter"] is not JsonObject) Add("perimeter section is missing");

        foreach (var path in SecretLiterals(raw, ""))
            Add($"{path}: a profile never contains a secret; use ${{setting:...}}");
    }

    private static void ValidateSource(string id, string kind, JsonObject s, Action<string> add)
    {
        string? Text(string k) => MonitorJson.ScalarText(s[k]);
        switch (kind)
        {
            case "log_files":
                if (s["paths"] is not JsonArray { Count: > 0 }) add($"source '{id}': paths must list at least one glob");
                var format = Text("format") ?? "jsonl";
                if (format is not ("jsonl" or "text")) add($"source '{id}': format must be jsonl or text");
                if (format == "text" && string.IsNullOrEmpty(Text("line_regex"))) add($"source '{id}': format text needs line_regex");
                if (Text("start_at") is { } start && start is not ("end" or "beginning")) add($"source '{id}': start_at must be end or beginning");
                break;
            case "log_analytics":
                var hasDiscovery = s["discovery"] is JsonObject d && d["tag_name"] is not null && d["tag_value"] is not null;
                if (!s.ContainsKey("workspace_id") && !hasDiscovery) add($"source '{id}': needs workspace_id or discovery with tag_name and tag_value");
                if (s["app_role_names"] is not JsonArray { Count: > 0 }) add($"source '{id}': app_role_names must list at least one role");
                break;
            case "http_health":
                if (string.IsNullOrEmpty(Text("base_url"))) add($"source '{id}': base_url is missing");
                break;
            case "sqlserver":
                if (string.IsNullOrEmpty(Text("connection_setting"))) add($"source '{id}': connection_setting is missing");
                if (Text("authentication") is not ("entra" or "sql")) add($"source '{id}': authentication must be entra or sql");
                break;
        }
    }

    private static IEnumerable<string> SecretLiterals(JsonNode? node, string path)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kv in obj)
                {
                    var child = path.Length == 0 ? kv.Key : path + "." + kv.Key;
                    if (IsSecretKey(kv.Key) && kv.Value is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 && !s.Contains("${setting:", StringComparison.Ordinal))
                        yield return child;
                    else
                        foreach (var inner in SecretLiterals(kv.Value, child)) yield return inner;
                }
                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                    foreach (var inner in SecretLiterals(arr[i], $"{path}[{i}]")) yield return inner;
                break;
        }
    }

    private static bool IsSecretKey(string key)
    {
        var k = key.ToLowerInvariant();
        return SecretKeyWords.Any(w => k == w || k.EndsWith("_" + w, StringComparison.Ordinal));
    }
}
