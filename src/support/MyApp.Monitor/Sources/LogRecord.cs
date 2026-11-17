using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MyApp.Monitor.Sources;

public enum RecordLevel { Trace, Debug, Information, Warning, Error, Critical }

/// <summary>The log record normal form (operations.md 6.1). Every source emits this shape.</summary>
public sealed record LogRecord
{
    public const string UnknownCategory = "unknown";

    private static readonly Regex LogKeyGrammar = new(@"^[a-z]{2,8}\.[a-z]{2,12}\.[a-z]+(_[a-z]+)*$", RegexOptions.CultureInvariant);

    public required string SourceId { get; init; }
    public string RecordId { get; init; } = "";
    public required DateTimeOffset Timestamp { get; init; }
    public RecordLevel Level { get; init; } = RecordLevel.Information;
    public string Category { get; init; } = UnknownCategory;
    public string? EventName { get; init; }
    public string? LogKey { get; init; }
    public string? MessageTemplate { get; init; }
    public string Message { get; init; } = "";
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
    public string? ExceptionType { get; init; }
    public string? ExceptionMessage { get; init; }
    public string? TopFrame { get; init; }
    public string? Version { get; init; }
    public string? Host { get; init; }
    public string? Service { get; init; }
    public required string Environment { get; init; }

    /// <summary>Where a person can find the record: the log file path for <c>log_files</c>. Not part of the record id; empty for other sources.</summary>
    public string? Origin { get; init; }

    /// <summary>True when the name is a log key (grammar of development.md 3.1) and not a platform or monitor event.</summary>
    public static bool IsLogKey(string? eventName) =>
        !string.IsNullOrEmpty(eventName)
        && !eventName.StartsWith("platform.", StringComparison.Ordinal)
        && !eventName.StartsWith("monitor.", StringComparison.Ordinal)
        && LogKeyGrammar.IsMatch(eventName);

    public static string ComputeRecordId(string sourceId, DateTimeOffset timestamp, string? traceId, string category, string message)
    {
        var text = string.Join('\n', sourceId, FormatTimestamp(timestamp), traceId ?? "", category, message);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..32];
    }

    public static string FormatTimestamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Builds a record, filling <see cref="RecordId"/> and <see cref="LogKey"/>, normalising ids to lower case.</summary>
    public static LogRecord Create(
        string sourceId, string environment, DateTimeOffset timestamp, RecordLevel level, string? category, string message,
        string? eventName = null, string? messageTemplate = null, string? traceId = null, string? spanId = null,
        IReadOnlyDictionary<string, string>? properties = null, string? exceptionType = null, string? exceptionMessage = null,
        string? topFrame = null, string? version = null, string? host = null, string? service = null)
    {
        var cat = string.IsNullOrWhiteSpace(category) ? UnknownCategory : category;
        var trace = string.IsNullOrWhiteSpace(traceId) ? null : traceId.ToLowerInvariant();
        var span = string.IsNullOrWhiteSpace(spanId) ? null : spanId.ToLowerInvariant();
        var name = string.IsNullOrWhiteSpace(eventName) ? null : eventName;
        return new LogRecord
        {
            SourceId = sourceId,
            Environment = environment,
            Timestamp = timestamp.ToUniversalTime(),
            Level = level,
            Category = cat,
            EventName = name,
            LogKey = IsLogKey(name) ? name : null,
            MessageTemplate = string.IsNullOrEmpty(messageTemplate) ? null : messageTemplate,
            Message = message,
            TraceId = trace,
            SpanId = span,
            Properties = properties ?? new Dictionary<string, string>(),
            ExceptionType = Nullable(exceptionType),
            ExceptionMessage = Nullable(exceptionMessage),
            TopFrame = Nullable(topFrame),
            Version = Nullable(version),
            Host = Nullable(host),
            Service = Nullable(service),
            RecordId = ComputeRecordId(sourceId, timestamp, trace, cat, message),
        };
    }

    private static string? Nullable(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    public string? Property(string name)
    {
        if (Properties.TryGetValue(name, out var v)) return v;
        foreach (var kv in Properties)
            if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)) return kv.Value;
        return null;
    }

    public static bool TryParseLevel(string? text, out RecordLevel level)
    {
        level = RecordLevel.Information;
        if (string.IsNullOrWhiteSpace(text)) return false;
        switch (text.Trim().ToLowerInvariant())
        {
            case "trace": case "verbose": case "vrb": case "trc": level = RecordLevel.Trace; return true;
            case "debug": case "dbg": level = RecordLevel.Debug; return true;
            case "information": case "info": case "inf": level = RecordLevel.Information; return true;
            case "warning": case "warn": case "wrn": level = RecordLevel.Warning; return true;
            case "error": case "err": level = RecordLevel.Error; return true;
            case "critical": case "crit": case "fatal": case "ftl": level = RecordLevel.Critical; return true;
            default: return false;
        }
    }

    /// <summary>Application Insights SeverityLevel: 0 Verbose, 1 Information, 2 Warning, 3 Error, 4 Critical.</summary>
    public static RecordLevel FromSeverityLevel(int severity) => severity switch
    {
        <= 0 => RecordLevel.Trace,
        1 => RecordLevel.Information,
        2 => RecordLevel.Warning,
        3 => RecordLevel.Error,
        _ => RecordLevel.Critical,
    };
}
