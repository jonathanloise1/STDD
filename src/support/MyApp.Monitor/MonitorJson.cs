using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MyApp.Monitor;

/// <summary>Shared JSON options and dot-path helpers. Contracts of the monitor use snake_case names.</summary>
public static class MonitorJson
{
    public static readonly JsonSerializerOptions Snake = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static readonly JsonSerializerOptions SnakeCompact = new(Snake) { WriteIndented = false };

    public static readonly JsonSerializerOptions Compact = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static readonly JsonDocumentOptions DocumentOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static JsonNode? Parse(string text) => JsonNode.Parse(text, documentOptions: DocumentOptions);

    public static string ToCompact(JsonNode? node) => node?.ToJsonString(Compact) ?? "null";

    public static JsonNode? ToNode<T>(T value) => JsonSerializer.SerializeToNode(value, Snake);

    /// <summary>
    /// Reads a dot path: object members (exact, then case-insensitive), array indexes as <c>name[0]</c> or as a numeric segment.
    /// Returns null when a segment is missing. A JSON null is returned as null too; use <see cref="TryGet"/> to tell them apart.
    /// </summary>
    public static JsonNode? Get(JsonNode? root, string path) => TryGet(root, path, out var v) ? v : null;

    public static bool TryGet(JsonNode? root, string path, out JsonNode? value)
    {
        value = root;
        if (path.Length == 0) return true;
        foreach (var rawSegment in SplitPath(path))
        {
            var segment = rawSegment;
            var indexes = new List<int>();
            var bracket = segment.IndexOf('[');
            if (bracket > 0 && segment.EndsWith(']'))
            {
                var rest = segment[bracket..];
                segment = segment[..bracket];
                foreach (var part in rest.Split(['[', ']'], StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var i)) { value = null; return false; }
                    indexes.Add(i);
                }
            }
            if (!Step(ref value, segment)) return false;
            foreach (var i in indexes)
            {
                if (value is JsonArray arr && i >= 0 && i < arr.Count) value = arr[i];
                else { value = null; return false; }
            }
        }
        return true;
    }

    private static IEnumerable<string> SplitPath(string path) => path.Split('.');

    private static bool Step(ref JsonNode? node, string segment)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj.TryGetPropertyValue(segment, out var exact)) { node = exact; return true; }
                foreach (var kv in obj)
                    if (string.Equals(kv.Key, segment, StringComparison.OrdinalIgnoreCase)) { node = kv.Value; return true; }
                node = null;
                return false;
            case JsonArray arr when int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var i):
                if (i < arr.Count) { node = arr[i]; return true; }
                node = null;
                return false;
            default:
                node = null;
                return false;
        }
    }

    /// <summary>Scalar text of a node, or null for null, objects and arrays.</summary>
    public static string? ScalarText(JsonNode? node)
    {
        if (node is not JsonValue v) return null;
        if (v.TryGetValue<string>(out var s)) return s;
        if (v.TryGetValue<bool>(out var b)) return b ? "true" : "false";
        if (v.TryGetValue<long>(out var l)) return l.ToString(CultureInfo.InvariantCulture);
        if (v.TryGetValue<double>(out var d)) return d.ToString("R", CultureInfo.InvariantCulture);
        if (v.TryGetValue<decimal>(out var m)) return m.ToString(CultureInfo.InvariantCulture);
        return v.ToJsonString(Compact).Trim('"');
    }

    /// <summary>A report as served by the API and downloaded from the Review page: indented, keys in the schema's order.</summary>
    public static string Report(JsonNode report) => report.ToJsonString(Snake);

    public static JsonNode? Clone(JsonNode? node) => node?.DeepClone();
}
