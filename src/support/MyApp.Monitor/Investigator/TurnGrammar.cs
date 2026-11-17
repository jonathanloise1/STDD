using System.Text;
using System.Text.Json.Nodes;

namespace MyApp.Monitor.Investigator;

/// <summary>
/// GBNF grammar of one investigator turn (operations.md 11.3), passed to llama.cpp's <c>grammar</c> request field: <c>&lt;call&gt;{...}&lt;/call&gt;</c>
/// with <c>arguments</c> constrained to the tool's parameter schema, or <c>&lt;report&gt;{...}&lt;/report&gt;</c> constrained to the investigation
/// definition, or only the report. A port of <c>training/investigator/grammar.py</c> (rule names and order included: the golden file pins the
/// text). It covers the schema subset of 7.1 and writes compact JSON; <c>pattern</c>, lengths, <c>minimum</c>, <c>maximum</c> and <c>maxItems</c>
/// are enforced by the validation after the report, not by the grammar.
/// </summary>
public sealed class TurnGrammar
{
    private static readonly Dictionary<string, string> Primitives = new()
    {
        ["string"] = "\"\\\"\" ([^\"\\\\\\x7F\\x00-\\x1F] | \"\\\\\" ([\"\\\\/bfnrt] | \"u\" [0-9a-fA-F]{4}))* \"\\\"\"",
        ["integer"] = "\"-\"? (\"0\" | [1-9] [0-9]*)",
        ["number"] = "\"-\"? (\"0\" | [1-9] [0-9]*) (\".\" [0-9]+)?",
        ["boolean"] = "\"true\" | \"false\"",
        ["null"] = "\"null\"",
    };

    private readonly List<string> _order = [];
    private readonly Dictionary<string, string> _rules = new(StringComparer.Ordinal);
    private readonly JsonObject _defs;
    private readonly IReadOnlyList<string> _entities;
    private int _n;

    private TurnGrammar(JsonObject defs, IReadOnlyList<string> entities)
    {
        _defs = defs;
        _entities = entities;
    }

    /// <param name="tools">The tools offered: name and parameter schema, in catalog order.</param>
    /// <param name="reportSchema">The investigation schema with its <c>$defs</c> (<see cref="Protocol.InvestigationSchema"/>).</param>
    /// <param name="entities">Entity names, the enum of <c>x-enum-from: entities</c>.</param>
    public static string Build(IEnumerable<(string Name, JsonNode Parameters)> tools, JsonObject reportSchema, IReadOnlyList<string> entities, bool onlyReport)
    {
        var b = new TurnGrammar(reportSchema["$defs"] as JsonObject ?? new JsonObject(), entities);
        var withoutDefs = new JsonObject();
        foreach (var kv in reportSchema) if (kv.Key != "$defs") withoutDefs[kv.Key] = kv.Value?.DeepClone();
        var report = b.Value(withoutDefs, "report");
        var reportRule = $"\"<report>\" {report} \"</report>\"";
        if (onlyReport) return b.Text(reportRule);
        var alternatives = new List<string>();
        foreach (var (name, parameters) in tools)
        {
            var args = b.Value(parameters, "args-" + name.Replace('_', '-'));
            alternatives.Add($"\"<call>{{\\\"tool\\\":\\\"{name}\\\",\\\"arguments\\\":\" {args} \"}}</call>\"");
        }
        alternatives.Add(reportRule);
        return b.Text(string.Join(" | ", alternatives));
    }

    private static string Lit(string text) => ProtocolJson.DumpsString(text);

    private void Set(string name, string body)
    {
        if (!_rules.ContainsKey(name)) _order.Add(name);
        _rules[name] = body;
    }

    private string Value(JsonNode schemaNode, string hint)
    {
        var schema = (JsonObject)schemaNode;
        if (schema["$ref"] is { } r)
        {
            var path = MonitorJson.ScalarText(r)!;
            var key = path[(path.LastIndexOf('/') + 1)..];
            var refName = "def-" + key.Replace('_', '-');
            if (!_rules.ContainsKey(refName))
            {
                Set(refName, "");                                   // reserved first: recursion guard, and the order the Python builder gives
                Set(refName, Body(_defs[key]!, refName));
            }
            return refName;
        }
        var name = $"{hint}-{++_n}";
        var body = Body(schema, name);                               // children are added before their parent
        Set(name, body);
        return name;
    }

    private string Body(JsonNode schemaNode, string name)
    {
        var schema = (JsonObject)schemaNode;
        if (schema["anyOf"] is JsonArray any) return string.Join(" | ", any.Select(s => Value(s!, "alt")));
        if (schema["enum"] is JsonArray en) return string.Join(" | ", en.Select(v => Lit(ProtocolJson.Dumps(v))));
        var type = schema["type"];
        if (type is JsonArray types)
        {
            return string.Join(" | ", types.Select(t =>
            {
                var copy = (JsonObject)schema.DeepClone();
                copy["type"] = MonitorJson.ScalarText(t);
                return Value(copy, "t");
            }));
        }
        var typeName = MonitorJson.ScalarText(type);
        if (typeName == "object") return ObjectBody(schema);
        if (typeName == "array")
        {
            var item = Value(schema["items"] ?? new JsonObject { ["type"] = "string" }, "item");
            return $"\"[\" ({item} (\",\" {item})*)? \"]\"";
        }
        if (MonitorJson.ScalarText(schema["x-enum-from"]) == "entities" && _entities.Count > 0)
            return string.Join(" | ", _entities.Select(e => Lit(ProtocolJson.DumpsString(e))));
        if (typeName is not null && Primitives.TryGetValue(typeName, out var primitive))
        {
            if (!_rules.ContainsKey(typeName)) Set(typeName, primitive);
            return typeName;
        }
        return "string";
    }

    private string ObjectBody(JsonObject schema)
    {
        var props = schema["properties"] as JsonObject ?? new JsonObject();
        if (props.Count == 0) return "\"{\" \"}\"";
        var requiredNames = (schema["required"] as JsonArray)?.Select(n => MonitorJson.ScalarText(n)!).ToHashSet(StringComparer.Ordinal) ?? [];
        var required = props.Select(kv => kv.Key).Where(requiredNames.Contains).ToList();
        var optional = props.Select(kv => kv.Key).Where(k => !requiredNames.Contains(k)).ToList();
        var parts = new List<string>();
        for (var i = 0; i < required.Count; i++)
        {
            var sub = Value(props[required[i]]!, required[i].Replace('_', '-'));
            parts.Add((i > 0 ? "\",\" " : "") + $"{Lit(ProtocolJson.DumpsString(required[i]) + ":")} {sub}");
        }
        var sb = new StringBuilder("\"{\" ").Append(string.Join(" ", parts));
        foreach (var k in optional)
        {
            var sub = Value(props[k]!, k.Replace('_', '-'));
            sb.Append(required.Count > 0
                ? $" (\",\" {Lit(ProtocolJson.DumpsString(k) + ":")} {sub})?"
                : $" ({Lit(ProtocolJson.DumpsString(k) + ":")} {sub})?");
        }
        return sb.Append(" \"}\"").ToString();
    }

    private string Text(string root)
    {
        var sb = new StringBuilder();
        sb.Append("root ::= ").Append(root).Append('\n');
        foreach (var name in _order) sb.Append(name).Append(" ::= ").Append(_rules[name]).Append('\n');
        return sb.ToString();
    }

    /// <summary>Names used on a right-hand side that no rule defines (a structural check; empty means well-formed).</summary>
    public static IReadOnlyList<string> UndefinedRules(string grammar)
    {
        var defined = new HashSet<string>(System.Text.RegularExpressions.Regex.Matches(grammar, @"^([A-Za-z][\w-]*) ::=", System.Text.RegularExpressions.RegexOptions.Multiline).Select(m => m.Groups[1].Value));
        var used = new HashSet<string>();
        foreach (var line in grammar.Split('\n'))
        {
            var at = line.IndexOf(" ::= ", StringComparison.Ordinal);
            var rhs = at < 0 ? "" : line[(at + 5)..];
            rhs = System.Text.RegularExpressions.Regex.Replace(rhs, "\"(?:\\\\.|[^\"\\\\])*\"", "");
            rhs = System.Text.RegularExpressions.Regex.Replace(rhs, @"\[(?:\\.|[^\]\\])*\]", "");
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(rhs, @"[A-Za-z][\w-]*")) used.Add(m.Value);
        }
        return used.Where(u => !defined.Contains(u)).OrderBy(u => u, StringComparer.Ordinal).ToList();
    }
}
