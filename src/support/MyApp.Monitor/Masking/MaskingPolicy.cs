using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MyApp.Monitor.Masking;

public enum MaskClass { Id, Code, Number, Boolean, Timestamp, FreeText, Hash, Entity, Pack }

public sealed record MaskPattern(string Name, Regex Regex, string Replacement);

public sealed record ProtectedPattern(string Name, Regex Regex);

/// <summary>The masking policy (operations.md 8.1), loaded from <c>masking-policy.json</c>.</summary>
public sealed class MaskingPolicy
{
    public int FreeTextMaxChars { get; init; } = 500;
    public IReadOnlyList<MaskPattern> Patterns { get; init; } = [];
    public IReadOnlyList<ProtectedPattern> Protected { get; init; } = [];
    public IReadOnlyDictionary<string, MaskClass> SignalFields { get; init; } = new Dictionary<string, MaskClass>();
    public IReadOnlyDictionary<string, MaskClass> SignalProperties { get; init; } = new Dictionary<string, MaskClass>();
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, MaskClass>> Tools { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, MaskClass>>();
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, MaskClass>> Entities { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, MaskClass>>();

    public static bool TryParseClass(string? text, out MaskClass cls)
    {
        cls = default;
        switch (text)
        {
            case "id": cls = MaskClass.Id; return true;
            case "code": cls = MaskClass.Code; return true;
            case "number": cls = MaskClass.Number; return true;
            case "boolean": cls = MaskClass.Boolean; return true;
            case "timestamp": cls = MaskClass.Timestamp; return true;
            case "free_text": cls = MaskClass.FreeText; return true;
            case "hash": cls = MaskClass.Hash; return true;
            case "entity": cls = MaskClass.Entity; return true;
            case "pack": cls = MaskClass.Pack; return true;
            default: return false;
        }
    }

    /// <summary>Class of a signal property: an exact name wins over a <c>*Suffix</c> glob (longest suffix first). Case-insensitive.</summary>
    public bool TryGetPropertyClass(string name, out MaskClass cls)
    {
        foreach (var kv in SignalProperties)
            if (!kv.Key.StartsWith('*') && string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) { cls = kv.Value; return true; }
        var best = -1;
        cls = default;
        foreach (var kv in SignalProperties)
        {
            if (!kv.Key.StartsWith('*')) continue;
            var suffix = kv.Key[1..];
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && suffix.Length > best) { best = suffix.Length; cls = kv.Value; }
        }
        return best >= 0;
    }

    public bool TryGetFieldClass(string name, out MaskClass cls)
    {
        foreach (var kv in SignalFields)
            if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) { cls = kv.Value; return true; }
        cls = default;
        return false;
    }
}

public static class MaskingPolicyLoader
{
    public static MaskingPolicy Load(string json, FindingList? findings = null)
    {
        findings ??= [];
        JsonNode? root;
        try { root = MonitorJson.Parse(json); }
        catch (Exception ex) { throw new MonitorException($"masking-policy.json is not valid JSON: {ex.Message}"); }
        var policy = Parse(root, findings);
        if (findings.Count > 0) throw new MonitorException("masking-policy.json is invalid: " + string.Join("; ", findings.Select(f => f.Message)));
        return policy;
    }

    /// <summary>Parses and validates; every problem is added to <paramref name="findings"/> (check O08).</summary>
    public static MaskingPolicy Parse(JsonNode? root, FindingList findings)
    {
        const string file = "masking-policy.json";
        if (root is not JsonObject obj) { findings.Add("O08", "must be a JSON object", file); return new MaskingPolicy(); }
        if (MonitorJson.ScalarText(obj["schema_version"]) != "1") findings.Add("O08", "schema_version must be 1", file);
        var maxChars = 500;
        if (obj["free_text_max_chars"] is JsonValue mv && mv.TryGetValue<int>(out var m) && m > 0) maxChars = m;
        else findings.Add("O08", "free_text_max_chars must be a positive integer", file);

        var patterns = new List<MaskPattern>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (obj["patterns"] is JsonArray pats)
        {
            for (var i = 0; i < pats.Count; i++)
            {
                if (pats[i] is not JsonObject p) { findings.Add("O08", $"patterns[{i}] must be an object", file); continue; }
                var name = MonitorJson.ScalarText(p["name"]);
                var regex = MonitorJson.ScalarText(p["regex"]);
                var repl = MonitorJson.ScalarText(p["replacement"]);
                if (string.IsNullOrEmpty(name) || regex is null || repl is null) { findings.Add("O08", $"patterns[{i}] needs name, regex and replacement", file); continue; }
                if (!seen.Add(name)) findings.Add("O08", $"pattern name '{name}' is duplicated", file);
                var options = RegexOptions.CultureInvariant;
                if (p["ignore_case"] is JsonValue ic && ic.TryGetValue<bool>(out var icb) && icb) options |= RegexOptions.IgnoreCase;
                try { patterns.Add(new MaskPattern(name, new Regex(regex, options, TimeSpan.FromSeconds(1)), repl)); }
                catch (ArgumentException ex) { findings.Add("O08", $"pattern '{name}' has an invalid regex: {ex.Message}", file); }
            }
        }
        else findings.Add("O08", "patterns must be an array", file);

        var prot = new List<ProtectedPattern>();
        if (obj["protected"] is JsonArray ps)
        {
            for (var i = 0; i < ps.Count; i++)
            {
                if (ps[i] is not JsonObject p) { findings.Add("O08", $"protected[{i}] must be an object", file); continue; }
                var name = MonitorJson.ScalarText(p["name"]) ?? $"protected{i}";
                var regex = MonitorJson.ScalarText(p["regex"]);
                if (regex is null) { findings.Add("O08", $"protected[{i}] needs a regex", file); continue; }
                try { prot.Add(new ProtectedPattern(name, new Regex(regex, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))); }
                catch (ArgumentException ex) { findings.Add("O08", $"protected '{name}' has an invalid regex: {ex.Message}", file); }
            }
        }
        else findings.Add("O08", "protected must be an array", file);

        var signalFields = new Dictionary<string, MaskClass>(StringComparer.OrdinalIgnoreCase);
        var signalProps = new Dictionary<string, MaskClass>(StringComparer.OrdinalIgnoreCase);
        if (obj["signal"] is JsonObject signal)
        {
            ReadClassMap(signal["fields"], signalFields, "signal.fields", findings, file);
            ReadClassMap(signal["properties"], signalProps, "signal.properties", findings, file);
        }
        else findings.Add("O08", "signal section is missing", file);

        var tools = new Dictionary<string, IReadOnlyDictionary<string, MaskClass>>(StringComparer.Ordinal);
        if (obj["tools"] is JsonObject toolsObj)
            foreach (var kv in toolsObj)
            {
                var map = new Dictionary<string, MaskClass>(StringComparer.OrdinalIgnoreCase);
                ReadClassMap(kv.Value, map, $"tools.{kv.Key}", findings, file);
                tools[kv.Key] = map;
            }
        else findings.Add("O08", "tools section is missing", file);

        var entities = new Dictionary<string, IReadOnlyDictionary<string, MaskClass>>(StringComparer.Ordinal);
        if (obj["entities"] is JsonObject entObj)
            foreach (var kv in entObj)
            {
                var map = new Dictionary<string, MaskClass>(StringComparer.OrdinalIgnoreCase);
                ReadClassMap(kv.Value, map, $"entities.{kv.Key}", findings, file);
                foreach (var c in map)
                    if (c.Value is MaskClass.Entity or MaskClass.Pack)
                        findings.Add("O08", $"entities.{kv.Key}.{c.Key}: class '{c.Value}' is not allowed in an entity section", file);
                entities[kv.Key] = map;
            }
        else findings.Add("O08", "entities section is missing", file);

        return new MaskingPolicy
        {
            FreeTextMaxChars = maxChars,
            Patterns = patterns,
            Protected = prot,
            SignalFields = signalFields,
            SignalProperties = signalProps,
            Tools = tools,
            Entities = entities,
        };
    }

    private static void ReadClassMap(JsonNode? node, Dictionary<string, MaskClass> target, string where, FindingList findings, string file)
    {
        if (node is not JsonObject obj) { findings.Add("O08", $"{where} must be an object", file); return; }
        foreach (var kv in obj)
        {
            var text = MonitorJson.ScalarText(kv.Value);
            if (!MaskingPolicy.TryParseClass(text, out var cls)) { findings.Add("O08", $"{where}.{kv.Key}: unknown class '{text}'", file); continue; }
            target[kv.Key] = cls;
        }
    }
}
