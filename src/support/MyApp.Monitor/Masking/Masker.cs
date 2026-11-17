using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyApp.Monitor.Sources;

namespace MyApp.Monitor.Masking;

public sealed record MaskedData(JsonNode? Data, int FieldsDropped);

/// <summary>The masking pass (operations.md section 8): allow-list by class, patterns on free text, HMAC for hashes.</summary>
public sealed class Masker
{
    public const string MaskedToken = "[masked]";

    private static readonly Regex GuidRegex = new(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.CultureInvariant);
    private static readonly Regex IntegerRegex = new(@"^-?\d{1,18}$", RegexOptions.CultureInvariant);
    private static readonly Regex HexIdRegex = new(@"^(?:[0-9a-fA-F]{32}|[0-9a-fA-F]{16})$", RegexOptions.CultureInvariant);
    private static readonly Regex CodeRegex = new(@"^[A-Za-z0-9_.:/{}+<>\[\]-]{1,200}$", RegexOptions.CultureInvariant);

    /// <summary>Result map of tools whose output masking section is <c>entities</c> (operations.md 7.1, 7.4).</summary>
    private static readonly IReadOnlyDictionary<string, MaskClass> EntityResultMap = new Dictionary<string, MaskClass>(StringComparer.OrdinalIgnoreCase)
    {
        ["entity"] = MaskClass.Code,
        ["id"] = MaskClass.Code,
        ["found"] = MaskClass.Boolean,
        ["fields"] = MaskClass.Entity,
    };

    private readonly byte[] _hashKey;

    public Masker(MaskingPolicy policy, byte[] hashKey)
    {
        Policy = policy;
        _hashKey = hashKey;
    }

    public MaskingPolicy Policy { get; }

    // ---- free text ----

    public string MaskFreeText(string text, int? maxChars = null)
    {
        var max = maxChars ?? Policy.FreeTextMaxChars;
        var spans = ProtectedSpans(text);
        var segments = new List<(string Text, bool Protected)>();
        var pos = 0;
        foreach (var (s, e) in spans)
        {
            if (s > pos) segments.Add((text[pos..s], false));
            segments.Add((text[s..e], true));
            pos = e;
        }
        if (pos < text.Length) segments.Add((text[pos..], false));

        foreach (var pattern in Policy.Patterns)
        {
            for (var i = 0; i < segments.Count; i++)
            {
                if (segments[i].Protected) continue;
                string replaced;
                try { replaced = pattern.Regex.Replace(segments[i].Text, pattern.Replacement); }
                catch (RegexMatchTimeoutException) { replaced = MaskedToken; }
                segments[i] = (replaced, false);
            }
        }
        var result = string.Concat(segments.Select(s => s.Text));
        return result.Length > max ? result[..max] : result;
    }

    private List<(int Start, int End)> ProtectedSpans(string text)
    {
        var raw = new List<(int, int)>();
        foreach (var p in Policy.Protected)
        {
            try
            {
                foreach (Match m in p.Regex.Matches(text))
                    if (m.Length > 0) raw.Add((m.Index, m.Index + m.Length));
            }
            catch (RegexMatchTimeoutException) { }
        }
        raw.Sort();
        var merged = new List<(int Start, int End)>();
        foreach (var (s, e) in raw)
        {
            if (merged.Count > 0 && s <= merged[^1].End) merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, e));
            else merged.Add((s, e));
        }
        return merged;
    }

    // ---- scalars ----

    public string Hash(string value)
    {
        var data = Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant());
        var mac = HMACSHA256.HashData(_hashKey, data);
        return "h:" + Convert.ToHexString(mac).ToLowerInvariant()[..12];
    }

    /// <summary>Applies a class to one scalar. Null stays null. A value that fails its class becomes <c>[masked]</c>.</summary>
    public JsonNode? MaskScalar(MaskClass cls, JsonNode? value)
    {
        if (value is null) return null;
        if (value is not JsonValue) return JsonValue.Create(MaskedToken);
        var text = MonitorJson.ScalarText(value) ?? "";
        switch (cls)
        {
            case MaskClass.Id:
                return GuidRegex.IsMatch(text) || IntegerRegex.IsMatch(text) || HexIdRegex.IsMatch(text) ? value.DeepClone() : Mask();
            case MaskClass.Code:
                return CodeRegex.IsMatch(text) ? value.DeepClone() : Mask();
            case MaskClass.Number:
                return IsNumber(value, text) ? value.DeepClone() : Mask();
            case MaskClass.Boolean:
                return IsBoolean(value, text) ? value.DeepClone() : Mask();
            case MaskClass.Timestamp:
                return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _) ? value.DeepClone() : Mask();
            case MaskClass.FreeText:
                return JsonValue.Create(MaskFreeText(text));
            case MaskClass.Hash:
                return JsonValue.Create(Hash(text));
            case MaskClass.Pack:
                return value.DeepClone();
            default:
                return Mask();
        }

        static JsonNode Mask() => JsonValue.Create(MaskedToken);
    }

    private static bool IsNumber(JsonNode node, string text)
    {
        if (node is JsonValue v && (v.TryGetValue<long>(out _) || v.TryGetValue<double>(out _) || v.TryGetValue<decimal>(out _)) && !v.TryGetValue<string>(out _))
            return true;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsBoolean(JsonNode node, string text)
    {
        if (node is JsonValue v && v.TryGetValue<bool>(out _)) return true;
        return text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    // ---- records (8.2: a source record, before analysis stores it) ----

    public LogRecord MaskRecord(LogRecord record, out int fieldsDropped)
    {
        var dropped = 0;
        string? Field(string name, string? value, bool allowNull = true)
        {
            if (value is null) return null;
            if (!Policy.TryGetFieldClass(name, out var cls)) { dropped++; return null; }
            return MonitorJson.ScalarText(MaskScalar(cls, JsonValue.Create(value)));
        }

        var props = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in record.Properties)
        {
            if (!Policy.TryGetPropertyClass(kv.Key, out var cls)) { dropped++; continue; }
            var masked = MonitorJson.ScalarText(MaskScalar(cls, JsonValue.Create(kv.Value)));
            if (masked is not null) props[kv.Key] = masked;
        }

        var masked2 = record with
        {
            SourceId = Field("SourceId", record.SourceId) ?? "",
            RecordId = Field("RecordId", record.RecordId) ?? "",
            Category = Field("Category", record.Category) ?? LogRecord.UnknownCategory,
            EventName = Field("EventName", record.EventName),
            LogKey = Field("LogKey", record.LogKey),
            MessageTemplate = Field("MessageTemplate", record.MessageTemplate),
            Message = Field("Message", record.Message) ?? "",
            TraceId = Field("TraceId", record.TraceId),
            SpanId = Field("SpanId", record.SpanId),
            ExceptionType = Field("ExceptionType", record.ExceptionType),
            ExceptionMessage = Field("ExceptionMessage", record.ExceptionMessage),
            TopFrame = Field("TopFrame", record.TopFrame),
            Version = Field("Version", record.Version),
            Host = Field("Host", record.Host),
            Service = Field("Service", record.Service),
            Environment = Field("Environment", record.Environment) ?? "",
            Properties = props,
        };
        // Level and Timestamp are typed values; their classes are checked, never rewritten.
        if (!Policy.TryGetFieldClass("Level", out _)) dropped++;
        if (!Policy.TryGetFieldClass("Timestamp", out _)) dropped++;
        fieldsDropped = dropped;
        return masked2;
    }

    // ---- tool results (8.2: every tool result before the envelope leaves the executor) ----

    /// <param name="section"><c>tools.{name}</c> or <c>entities</c>.</param>
    /// <param name="entity">Entity named in the call, for the <c>entity</c> class.</param>
    public MaskedData MaskToolData(JsonNode? data, string section, string? entity)
    {
        IReadOnlyDictionary<string, MaskClass>? map;
        if (section == "entities") map = EntityResultMap;
        else if (section.StartsWith("tools.", StringComparison.Ordinal) && Policy.Tools.TryGetValue(section[6..], out var m)) map = m;
        else map = null;
        if (map is null) return new MaskedData(null, CountLeaves(data));

        var dropped = 0;
        var result = Walk(data, "", map, entity, ref dropped, out var keep);
        return new MaskedData(keep ? result : null, dropped);
    }

    private JsonNode? Walk(JsonNode? node, string path, IReadOnlyDictionary<string, MaskClass> map, string? entity, ref int dropped, out bool keep)
    {
        keep = true;
        if (path.Length > 0 && map.TryGetValue(path, out var cls) && cls is MaskClass.Entity or MaskClass.Pack)
        {
            if (cls == MaskClass.Pack) return node?.DeepClone();
            return MaskEntityObject(node, entity, ref dropped);
        }

        switch (node)
        {
            case JsonObject obj:
            {
                var copy = new JsonObject();
                foreach (var kv in obj)
                {
                    var child = Walk(kv.Value, path.Length == 0 ? kv.Key : path + "." + kv.Key, map, entity, ref dropped, out var k);
                    if (k) copy[kv.Key] = child;
                }
                return copy;
            }
            case JsonArray arr:
            {
                var copy = new JsonArray();
                var itemPath = path + "[]";
                foreach (var item in arr)
                {
                    var child = Walk(item, itemPath, map, entity, ref dropped, out var k);
                    if (k) copy.Add(child);
                }
                return copy;
            }
            default:
                if (path.Length > 0 && map.TryGetValue(path, out var scalarClass)) return MaskScalar(scalarClass, node);
                dropped++;
                keep = false;
                return null;
        }
    }

    private JsonNode? MaskEntityObject(JsonNode? node, string? entity, ref int dropped)
    {
        if (node is null) return null;
        if (node is JsonValue v && v.TryGetValue<string>(out var text))
        {
            try { node = MonitorJson.Parse(text); } catch (System.Text.Json.JsonException) { node = null; }
        }
        if (node is not JsonObject obj) return new JsonObject();

        IReadOnlyDictionary<string, MaskClass>? section = null;
        if (entity is not null) Policy.Entities.TryGetValue(entity, out section);
        var result = new JsonObject();
        foreach (var kv in obj)
        {
            if (section is null || !section.TryGetValue(kv.Key, out var cls)) { dropped++; continue; }
            var value = kv.Value is JsonValue or null ? kv.Value : JsonValue.Create(MonitorJson.ToCompact(kv.Value));
            result[kv.Key] = MaskScalar(cls, value);
        }
        return result;
    }

    private static int CountLeaves(JsonNode? node) => node switch
    {
        JsonObject o => o.Sum(kv => CountLeaves(kv.Value)),
        JsonArray a => a.Sum(CountLeaves),
        null => 0,
        _ => 1,
    };

    // ---- tool arguments (8.2: written to the audit row) ----

    public string MaskArgumentValue(string name, string value)
    {
        if (Policy.TryGetPropertyClass(name, out var cls) && cls != MaskClass.Entity && cls != MaskClass.Pack)
            return MonitorJson.ScalarText(MaskScalar(cls, JsonValue.Create(value))) ?? MaskedToken;
        return MaskFreeText(value);
    }

    /// <summary>Masks every scalar of an arguments object for the audit row; keys are kept.</summary>
    public JsonObject MaskArguments(JsonObject? arguments)
    {
        var result = new JsonObject();
        if (arguments is null) return result;
        foreach (var kv in arguments)
        {
            var text = kv.Value is JsonValue ? MonitorJson.ScalarText(kv.Value) : MonitorJson.ToCompact(kv.Value);
            result[kv.Key] = text is null ? null : JsonValue.Create(MaskArgumentValue(kv.Key, text));
        }
        return result;
    }

    /// <summary>Second line of defence for report text written by the investigator (patterns only).</summary>
    public string MaskReportText(string text) => MaskFreeText(text, int.MaxValue);
}
