using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyApp.Monitor.Investigator;

/// <summary>
/// Compact JSON exactly as the Python side writes it for a turn (<c>json.dumps(obj, separators=(",", ":"), ensure_ascii=False)</c>): no
/// whitespace, non-ASCII characters kept, only quotes, backslashes and control characters escaped, member order as given. The golden file
/// <c>training/investigator/protocol.golden.json</c> is the contract; <c>System.Text.Json</c> cannot be used because it writes quotes as
/// <c>"</c> and escapes more characters.
/// </summary>
public static class ProtocolJson
{
    public static string Dumps(JsonNode? node)
    {
        var sb = new StringBuilder();
        Write(sb, node);
        return sb.ToString();
    }

    public static string DumpsString(string value)
    {
        var sb = new StringBuilder();
        WriteString(sb, value);
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, JsonNode? node)
    {
        switch (node)
        {
            case null:
                sb.Append("null");
                break;
            case JsonObject obj:
                sb.Append('{');
                var first = true;
                foreach (var kv in obj)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteString(sb, kv.Key);
                    sb.Append(':');
                    Write(sb, kv.Value);
                }
                sb.Append('}');
                break;
            case JsonArray arr:
                sb.Append('[');
                for (var i = 0; i < arr.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    Write(sb, arr[i]);
                }
                sb.Append(']');
                break;
            case JsonValue v:
                switch (v.GetValueKind())
                {
                    case JsonValueKind.String: WriteString(sb, v.GetValue<string>()); break;
                    case JsonValueKind.True: sb.Append("true"); break;
                    case JsonValueKind.False: sb.Append("false"); break;
                    case JsonValueKind.Null: sb.Append("null"); break;
                    default: sb.Append(Number(v.ToJsonString())); break;
                }
                break;
        }
    }

    private static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case < ' ': sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
    }

    /// <summary>An integer as written; a number with a fraction or exponent as Python's <c>repr(float)</c> would print it.</summary>
    private static string Number(string raw)
    {
        if (raw.Length > 0 && raw.All(c => c is >= '0' and <= '9' or '-')) return raw;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) || double.IsNaN(d) || double.IsInfinity(d)) return raw;
        return PythonFloat(d);
    }

    public static string PythonFloat(double d)
    {
        if (d == 0) return double.IsNegative(d) ? "-0.0" : "0.0";
        // the shortest digits that round-trip come from "R"; rebuild the decimal exponent from them
        var shortest = Math.Abs(d).ToString("R", CultureInfo.InvariantCulture);
        var exp = 0;
        var mantissa = shortest;
        var e = shortest.IndexOfAny(['E', 'e']);
        if (e >= 0)
        {
            mantissa = shortest[..e];
            exp = int.Parse(shortest[(e + 1)..], CultureInfo.InvariantCulture);
        }
        var dot = mantissa.IndexOf('.');
        var digits = mantissa.Replace(".", "");
        var pointPos = (dot < 0 ? mantissa.Length : dot) + exp;           // digits before the decimal point
        var lead = digits.Length - digits.TrimStart('0').Length;
        digits = digits.TrimStart('0');
        pointPos -= lead;
        digits = digits.TrimEnd('0');
        if (digits.Length == 0) digits = "0";
        var sci = pointPos - 1;                                           // scientific exponent
        string text;
        if (sci is >= -4 and < 16)
        {
            if (pointPos <= 0) text = "0." + new string('0', -pointPos) + digits;
            else if (digits.Length <= pointPos) text = digits + new string('0', pointPos - digits.Length) + ".0";
            else text = digits[..pointPos] + "." + digits[pointPos..];
        }
        else
        {
            var m = digits.Length == 1 ? digits : digits[0] + "." + digits[1..];
            text = m + "e" + (sci < 0 ? "-" : "+") + Math.Abs(sci).ToString("00", CultureInfo.InvariantCulture);
        }
        return d < 0 ? "-" + text : text;
    }
}
