using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MyApp.Monitor.Rules;

/// <summary>
/// An expectation of a runbook rule check (operations.md 5.4): <c>{path} {op} {literal}</c> with op one of
/// <c>== != &gt; &gt;= &lt; &lt;=</c>, or <c>{path} exists</c>, or <c>{path} absent</c>.
/// </summary>
public sealed record Expectation(string Path, string Operator, JsonNode? Literal)
{
    private static readonly Regex Compare = new(@"^(?<path>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z0-9_]+|\[\d+\])*)\s+(?<op>==|!=|>=|<=|>|<)\s+(?<lit>.+)$", RegexOptions.CultureInvariant);
    private static readonly Regex Presence = new(@"^(?<path>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z0-9_]+|\[\d+\])*)\s+(?<op>exists|absent)$", RegexOptions.CultureInvariant);

    public static bool TryParse(string text, out Expectation? expectation, out string error)
    {
        expectation = null;
        error = "";
        var t = text.Trim();
        var p = Presence.Match(t);
        if (p.Success)
        {
            expectation = new Expectation(p.Groups["path"].Value, p.Groups["op"].Value, null);
            return true;
        }
        var m = Compare.Match(t);
        if (!m.Success) { error = $"'{text}' is not '{{path}} {{op}} {{literal}}', '{{path}} exists' or '{{path}} absent'"; return false; }
        var lit = m.Groups["lit"].Value.Trim();
        JsonNode? literal;
        if (lit.Length >= 2 && lit[0] == '"' && lit[^1] == '"') literal = JsonValue.Create(lit[1..^1]);
        else if (lit == "true") literal = JsonValue.Create(true);
        else if (lit == "false") literal = JsonValue.Create(false);
        else if (lit == "null") literal = null;
        else if (double.TryParse(lit, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            literal = long.TryParse(lit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? JsonValue.Create(l) : JsonValue.Create(d);
        else { error = $"literal '{lit}' must be a quoted string, a number, true, false or null"; return false; }
        expectation = new Expectation(m.Groups["path"].Value, m.Groups["op"].Value, literal);
        return true;
    }

    /// <summary>
    /// Evaluates against the tool result <c>data</c>. A missing path fails every comparison; only <c>absent</c> passes on it.
    /// </summary>
    public bool Evaluate(JsonNode? data)
    {
        var found = MonitorJson.TryGet(data, Path, out var value);
        var present = found && value is not null;
        switch (Operator)
        {
            case "exists": return present;
            case "absent": return !present;
        }
        if (!found) return false;
        switch (Operator)
        {
            case "==": return Equal(value, Literal);
            case "!=": return !Equal(value, Literal);
        }
        if (!TryNumber(value, out var left) || !TryNumber(Literal, out var right)) return false;
        return Operator switch
        {
            ">" => left > right,
            ">=" => left >= right,
            "<" => left < right,
            "<=" => left <= right,
            _ => false,
        };
    }

    private static bool Equal(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (TryNumber(a, out var x) && TryNumber(b, out var y) && !IsString(a) && !IsString(b)) return x == y;
        if (a is JsonValue va && b is JsonValue vb && va.TryGetValue<bool>(out var ba) && vb.TryGetValue<bool>(out var bb)) return ba == bb;
        return string.Equals(MonitorJson.ScalarText(a), MonitorJson.ScalarText(b), StringComparison.Ordinal);
    }

    private static bool IsString(JsonNode n) => n is JsonValue v && v.TryGetValue<string>(out _);

    private static bool TryNumber(JsonNode? n, out double d)
    {
        d = 0;
        if (n is not JsonValue v) return false;
        if (v.TryGetValue<double>(out d)) return true;
        if (v.TryGetValue<long>(out var l)) { d = l; return true; }
        if (v.TryGetValue<string>(out var s)) return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
        return false;
    }
}
