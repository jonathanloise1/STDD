using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MyApp.Monitor.Masking;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;

namespace MyApp.Monitor.Tools;

/// <summary>Everything the working example tools need. Tests build it with fakes; the host builds it from the pack and configuration.</summary>
public sealed record ToolServices(
    EntityCatalog Entities,
    MaskingPolicy Masking,
    IPackProvider? Pack,
    ISqlReadRunner? Sql,
    ILogAnalyticsClient? LogAnalytics,
    IWorkspaceResolver? Workspaces,
    IHttpClientFactory? Http,
    string ContentRoot,
    TimeProvider Time);

public interface IWorkspaceResolver
{
    /// <summary>The workspace to query for a <c>log_analytics</c> source: configured, or discovered by tag (operations.md 6.3).</summary>
    Task<string> ResolveAsync(string sourceId, JsonObject sourceSettings, CancellationToken ct);
}

public static class BuiltInTools
{
    /// <summary>The five working example tools (operations.md 7.4) for every source kind they list.</summary>
    public static IReadOnlyList<IToolImplementation> Create(ToolServices s) =>
    [
        new LogsByCorrelationLogAnalytics(s),
        new LogsByCorrelationLogFiles(s),
        new LogsByCorrelationWorld(s),
        new SqlEntityReadSqlServer(s),
        new SqlEntityReadWorld(s),
        new AuditTrailSqlServer(s),
        new AuditTrailWorld(s),
        new HealthStatusHttp(s),
        new HealthStatusWorld(),
        new SignatureLookupPack(s),
    ];
}

internal static class ToolValues
{
    public static JsonNode? FromObject(object? value) => value switch
    {
        null or DBNull => null,
        JsonNode n => n.DeepClone(),
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        short sh => JsonValue.Create((int)sh),
        byte by => JsonValue.Create((int)by),
        double d => JsonValue.Create(d),
        float f => JsonValue.Create((double)f),
        decimal m => JsonValue.Create(m),
        Guid g => JsonValue.Create(g.ToString("D")),
        DateTime dt => JsonValue.Create(new DateTimeOffset(dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt).UtcDateTime
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)),
        DateTimeOffset dto => JsonValue.Create(dto.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)),
        byte[] bytes => JsonValue.Create(Convert.ToHexString(bytes).ToLowerInvariant()),
        JsonElement e => JsonNode.Parse(e.GetRawText()),
        IReadOnlyDictionary<string, object?> dict => new JsonObject(dict.Select(kv => KeyValuePair.Create(kv.Key, FromObject(kv.Value)))),
        _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };

    public static JsonObject Row(IReadOnlyDictionary<string, object?> row)
    {
        var o = new JsonObject();
        foreach (var kv in row) o[kv.Key] = FromObject(kv.Value);
        return o;
    }

    public static int Limit(JsonObject args, string name, int fallback, int max)
    {
        var requested = args[name] is JsonValue v && v.TryGetValue<int>(out var i) ? i : fallback;
        return Math.Clamp(requested, 1, max);
    }

    public static string Required(JsonObject args, string name) =>
        MonitorJson.ScalarText(args[name]) ?? throw new ToolArgumentException($"'{name}' is required");

    public static JsonNode? ParseJsonText(JsonNode? node)
    {
        if (node is JsonValue v && v.TryGetValue<string>(out var text) && text.Length > 0)
        {
            try { return JsonNode.Parse(text); } catch (JsonException) { return node; }
        }
        return node;
    }
}

/// <summary>Searches JSON-lines files for the lines of one trace id (<c>log_files</c> and <c>world</c> implementations of logs_by_correlation).</summary>
internal static class JsonlTraceSearch
{
    /// <summary>Same mapping as the <c>log_files</c> implementation of the catalog; used by <c>world</c>.</summary>
    public static readonly JsonObject DefaultColumns = new()
    {
        ["timestamp"] = "Timestamp",
        ["kind"] = "=trace",
        ["level"] = "Level",
        ["category"] = "Category",
        ["log_key"] = "LogKey",
        ["template"] = "MessageTemplate",
        ["message"] = "Message",
        ["span_id"] = "SpanId",
        ["exception_type"] = "Properties.ExceptionType|Exception.Type",
        ["top_frame"] = "Properties.TopFrame|Exception.Frames[0]",
    };

    public static List<JsonObject> Search(
        IEnumerable<FileInfo> filesNewestFirst, string traceId, string matchField, JsonObject columns, int limit, DateTimeOffset? since)
    {
        var rows = new List<(DateTimeOffset At, JsonObject Row)>();
        foreach (var file in filesNewestFirst)
        {
            if (rows.Count >= limit) break;
            if (since is { } s && file.LastWriteTimeUtc < s.UtcDateTime) continue;
            try
            {
                using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, new UTF8Encoding(false));
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    if (line.Length == 0 || !line.Contains(traceId, StringComparison.Ordinal)) continue;
                    JsonNode? node;
                    try { node = JsonNode.Parse(line); } catch (JsonException) { continue; }
                    if (node is not JsonObject obj) continue;
                    if (!string.Equals(MonitorJson.ScalarText(MonitorJson.Get(obj, matchField)), traceId, StringComparison.Ordinal)) continue;
                    var at = DateTimeOffset.TryParse(MonitorJson.ScalarText(obj["Timestamp"]), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : DateTimeOffset.MinValue;
                    if (since is { } cutoff && at < cutoff) continue;
                    rows.Add((at, JsonColumns.Map(obj, columns)));
                    if (rows.Count >= limit) break;
                }
            }
            catch (IOException) { /* a rotated or locked file is skipped */ }
        }
        return rows.OrderBy(r => r.At).Select(r => r.Row).ToList();
    }
}
