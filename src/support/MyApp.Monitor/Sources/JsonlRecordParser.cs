using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyApp.Monitor.Sources;

/// <summary>Maps a line of the JSON lines log file (operations.md 3.4) to a log record (6.1).</summary>
public static class JsonlRecordParser
{
    public static LogRecord? Parse(string line, string sourceId, string environment, bool truncated = false)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(line); }
        catch (JsonException) { return truncated ? Fallback(line, sourceId, environment) : null; }
        if (node is not JsonObject obj) return null;

        var timestamp = DateTimeOffset.TryParse(Text(obj, "Timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)
            ? t : DateTimeOffset.UtcNow;
        LogRecord.TryParseLevel(Text(obj, "Level"), out var level);

        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (obj["Properties"] is JsonObject props)
            foreach (var kv in props)
                properties[kv.Key] = kv.Value is JsonValue ? MonitorJson.ScalarText(kv.Value) ?? "" : MonitorJson.ToCompact(kv.Value);
        if (truncated) properties["Truncated"] = "true";

        var exception = obj["Exception"] as JsonObject;
        var eventName = Text(obj, "LogKey") ?? properties.GetValueOrDefault("LogKey");
        var message = Text(obj, "Message") ?? Text(obj, "MessageTemplate") ?? "";
        var topFrame = properties.GetValueOrDefault("TopFrame") ?? (exception?["Frames"] as JsonArray)?.Select(f => MonitorJson.ScalarText(f)).FirstOrDefault();

        return LogRecord.Create(
            sourceId, environment, timestamp, level, Text(obj, "Category"), message,
            eventName: eventName,
            messageTemplate: Text(obj, "MessageTemplate"),
            traceId: Text(obj, "TraceId"),
            spanId: Text(obj, "SpanId"),
            properties: properties,
            exceptionType: properties.GetValueOrDefault("ExceptionType") ?? Text(exception, "Type"),
            exceptionMessage: Text(exception, "Message"),
            topFrame: topFrame,
            version: Text(obj, "Version"),
            host: Text(obj, "Host"),
            service: Text(obj, "Service"));
    }

    private static string? Text(JsonObject? obj, string name) => obj is null ? null : MonitorJson.ScalarText(obj[name]);

    /// <summary>A line over 64 KB that was cut is no longer JSON: it is kept as a flagged warning so it is counted.</summary>
    private static LogRecord Fallback(string line, string sourceId, string environment) => LogRecord.Create(
        sourceId, environment, DateTimeOffset.UtcNow, RecordLevel.Warning, null,
        line.Length > 1000 ? line[..1000] : line,
        properties: new Dictionary<string, string> { ["Truncated"] = "true" });
}
