using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyApp.Monitor.Tools;

/// <summary>
/// Column mapping of tool implementations (operations.md 7.1): a value is a source field or dot path (<c>Properties.LogKey</c>),
/// <c>a|b</c> takes the first non-empty, <c>=text</c> is a constant.
/// </summary>
public static class JsonColumns
{
    public static JsonNode? Resolve(JsonNode? row, string expression)
    {
        foreach (var option in expression.Split('|'))
        {
            var opt = option.Trim();
            if (opt.StartsWith('=')) return JsonValue.Create(opt[1..]);
            var value = Lookup(row, opt);
            if (value is null) continue;
            if (value is JsonValue v && v.TryGetValue<string>(out var s) && s.Length == 0) continue;
            return value.DeepClone();
        }
        return null;
    }

    private static JsonNode? Lookup(JsonNode? row, string path)
    {
        // A dynamic column may arrive as a JSON string ("Properties": "{...}"): expand it when the path goes through it.
        var segments = path.Split('.', 2);
        if (segments.Length == 2 && MonitorJson.TryGet(row, segments[0], out var head) && head is JsonValue hv && hv.TryGetValue<string>(out var text)
            && text.TrimStart().StartsWith('{'))
        {
            try { return MonitorJson.Get(JsonNode.Parse(text), segments[1]); }
            catch (JsonException) { return null; }
        }
        return MonitorJson.Get(row, path);
    }

    public static JsonObject Map(JsonNode? row, JsonObject columns)
    {
        var result = new JsonObject();
        foreach (var kv in columns)
        {
            var expression = MonitorJson.ScalarText(kv.Value);
            if (expression is null) continue;
            result[kv.Key] = Resolve(row, expression);
        }
        return result;
    }
}
