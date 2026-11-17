using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MyApp.Monitor.Tools;

/// <summary>
/// Validator for the JSON Schema subset of operations.md 7.1: type, properties, required, additionalProperties,
/// enum, pattern, minimum, maximum, minLength, maxLength, items, maxItems, anyOf, $ref to #/$defs, annotations,
/// and the extension <c>x-enum-from</c>.
/// </summary>
public sealed class SchemaSubsetValidator
{
    public static readonly IReadOnlySet<string> AllowedKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "type", "properties", "required", "additionalProperties", "enum", "pattern", "minimum", "maximum", "minLength", "maxLength",
        "items", "maxItems", "anyOf", "$ref", "$schema", "$id", "title", "description", "$defs", "x-enum-from",
    };

    private readonly JsonNode _root;
    private readonly Func<string, IReadOnlyCollection<string>?> _enumFrom;

    public SchemaSubsetValidator(JsonNode rootSchema, Func<string, IReadOnlyCollection<string>?>? enumFrom = null)
    {
        _root = rootSchema;
        _enumFrom = enumFrom ?? (_ => null);
    }

    /// <summary>Returns one message per keyword outside the subset, with its location in the schema.</summary>
    public static List<string> CheckSubset(JsonNode? schema, string path = "$")
    {
        var problems = new List<string>();
        Walk(schema, path, problems, inPropertiesMap: false);
        return problems;
    }

    private static void Walk(JsonNode? node, string path, List<string> problems, bool inPropertiesMap)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kv in obj)
                {
                    if (inPropertiesMap) { Walk(kv.Value, $"{path}.{kv.Key}", problems, false); continue; }
                    if (!AllowedKeywords.Contains(kv.Key)) { problems.Add($"{path}: keyword '{kv.Key}' is outside the schema subset"); continue; }
                    switch (kv.Key)
                    {
                        case "properties":
                        case "$defs":
                            Walk(kv.Value, $"{path}.{kv.Key}", problems, inPropertiesMap: true);
                            break;
                        case "items":
                        case "additionalProperties":
                            Walk(kv.Value, $"{path}.{kv.Key}", problems, false);
                            break;
                        case "anyOf":
                            if (kv.Value is JsonArray arr)
                                for (var i = 0; i < arr.Count; i++) Walk(arr[i], $"{path}.anyOf[{i}]", problems, false);
                            break;
                    }
                }
                break;
        }
    }

    public List<string> Validate(JsonNode? instance)
    {
        var errors = new List<string>();
        ValidateNode(_root, instance, "$", errors, 0);
        return errors;
    }

    public bool IsValid(JsonNode? instance, out List<string> errors)
    {
        errors = Validate(instance);
        return errors.Count == 0;
    }

    private void ValidateNode(JsonNode? schemaNode, JsonNode? value, string path, List<string> errors, int depth)
    {
        if (depth > 40) { errors.Add($"{path}: schema nesting too deep"); return; }
        if (schemaNode is JsonValue bv && bv.TryGetValue<bool>(out var allowAll)) { if (!allowAll) errors.Add($"{path}: not allowed"); return; }
        if (schemaNode is not JsonObject schema) return;

        if (schema["$ref"] is JsonValue refValue && refValue.TryGetValue<string>(out var reference))
        {
            var target = Resolve(reference);
            if (target is null) { errors.Add($"{path}: unresolved $ref '{reference}'"); return; }
            ValidateNode(target, value, path, errors, depth + 1);
            return;
        }

        if (schema["anyOf"] is JsonArray anyOf)
        {
            var passed = false;
            foreach (var option in anyOf)
            {
                var sub = new List<string>();
                ValidateNode(option, value, path, sub, depth + 1);
                if (sub.Count == 0) { passed = true; break; }
            }
            if (!passed) errors.Add($"{path}: does not match any allowed shape");
            return;
        }

        if (schema["type"] is { } typeNode && !TypeMatches(typeNode, value))
        {
            errors.Add($"{path}: expected {DescribeType(typeNode)}, found {Kind(value)}");
            return;
        }

        if (schema["enum"] is JsonArray enumValues && !enumValues.Any(e => SameScalar(e, value)))
            errors.Add($"{path}: '{MonitorJson.ScalarText(value)}' is not one of {string.Join(", ", enumValues.Select(e => MonitorJson.ScalarText(e)))}");

        if (schema["x-enum-from"] is JsonValue xf && xf.TryGetValue<string>(out var source) && value is JsonValue)
        {
            var allowed = _enumFrom(source);
            var text = MonitorJson.ScalarText(value);
            if (allowed is null) errors.Add($"{path}: enum source '{source}' is not available");
            else if (text is null || !allowed.Contains(text)) errors.Add($"{path}: '{text}' is not a known {source} name");
        }

        if (value is JsonValue)
        {
            var text = MonitorJson.ScalarText(value);
            if (IsString(value))
            {
                if (schema["pattern"] is JsonValue pv && pv.TryGetValue<string>(out var pattern))
                {
                    try { if (!Regex.IsMatch(text ?? "", pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) errors.Add($"{path}: '{Shorten(text)}' does not match {pattern}"); }
                    catch (ArgumentException) { errors.Add($"{path}: invalid pattern {pattern}"); }
                }
                if (Int(schema["minLength"]) is { } minLen && (text?.Length ?? 0) < minLen) errors.Add($"{path}: shorter than {minLen}");
                if (Int(schema["maxLength"]) is { } maxLen && (text?.Length ?? 0) > maxLen) errors.Add($"{path}: longer than {maxLen}");
            }
            else if (Kind(value) == "number")
            {
                var number = double.Parse(text!, CultureInfo.InvariantCulture);
                if (Num(schema["minimum"]) is { } min && number < min) errors.Add($"{path}: {text} is below {min}");
                if (Num(schema["maximum"]) is { } max && number > max) errors.Add($"{path}: {text} is above {max}");
            }
        }

        if (value is JsonObject obj)
        {
            var props = schema["properties"] as JsonObject;
            if (schema["required"] is JsonArray required)
                foreach (var r in required)
                {
                    var name = MonitorJson.ScalarText(r);
                    if (name is not null && !obj.ContainsKey(name)) errors.Add($"{path}: missing required '{name}'");
                }
            foreach (var kv in obj)
            {
                if (props is not null && props.TryGetPropertyValue(kv.Key, out var propSchema))
                    ValidateNode(propSchema, kv.Value, $"{path}.{kv.Key}", errors, depth + 1);
                else if (schema["additionalProperties"] is JsonValue ap && ap.TryGetValue<bool>(out var allowed) && !allowed)
                    errors.Add($"{path}: unknown property '{kv.Key}'");
                else if (schema["additionalProperties"] is JsonObject apSchema)
                    ValidateNode(apSchema, kv.Value, $"{path}.{kv.Key}", errors, depth + 1);
            }
        }

        if (value is JsonArray arr)
        {
            if (Int(schema["maxItems"]) is { } maxItems && arr.Count > maxItems) errors.Add($"{path}: more than {maxItems} items");
            if (schema["items"] is { } itemSchema)
                for (var i = 0; i < arr.Count; i++) ValidateNode(itemSchema, arr[i], $"{path}[{i}]", errors, depth + 1);
        }
    }

    private JsonNode? Resolve(string reference)
    {
        const string prefix = "#/$defs/";
        if (!reference.StartsWith(prefix, StringComparison.Ordinal)) return null;
        return _root["$defs"]?[reference[prefix.Length..]];
    }

    private static string Shorten(string? s) => s is null ? "" : s.Length > 60 ? s[..60] + "..." : s;

    private static int? Int(JsonNode? n) => n is JsonValue v && v.TryGetValue<int>(out var i) ? i : null;
    private static double? Num(JsonNode? n) => n is null ? null : double.TryParse(MonitorJson.ScalarText(n), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static bool IsString(JsonNode? n) => n is not null && n.GetValueKind() == JsonValueKind.String;

    private static string Kind(JsonNode? n) => n is null ? "null" : n.GetValueKind() switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        _ => "null",
    };

    private static bool TypeMatches(JsonNode typeNode, JsonNode? value)
    {
        if (typeNode is JsonArray arr) return arr.Any(t => TypeMatches(t!, value));
        var type = MonitorJson.ScalarText(typeNode);
        var kind = Kind(value);
        return type switch
        {
            "integer" => kind == "number" && double.TryParse(MonitorJson.ScalarText(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && Math.Abs(d % 1) < double.Epsilon,
            "number" => kind == "number",
            _ => type == kind,
        };
    }

    private static string DescribeType(JsonNode t) => t is JsonArray a ? string.Join(" or ", a.Select(x => MonitorJson.ScalarText(x))) : MonitorJson.ScalarText(t) ?? "?";

    private static bool SameScalar(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null) return a is null && b is null;
        return Kind(a) == Kind(b) && MonitorJson.ScalarText(a) == MonitorJson.ScalarText(b);
    }
}
