using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace MyApp.Monitor.Sources;

/// <summary>What a source needs to start: its profile entry, the environment, the state tables and the clock.</summary>
public sealed record SourceContext(
    string SourceId,
    string Kind,
    string Environment,
    JsonObject Settings,
    string ContentRoot,
    ISourceStateStore State,
    TimeProvider Time,
    ILogger? Logger = null)
{
    public string? GetString(string name) => MonitorJson.ScalarText(Settings[name]);

    public int GetInt(string name, int fallback) =>
        int.TryParse(GetString(name), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;

    public IReadOnlyList<string> GetStrings(string name) => Settings[name] switch
    {
        JsonArray a => a.Select(n => MonitorJson.ScalarText(n) ?? "").Where(s => s.Length > 0).ToList(),
        JsonValue v when MonitorJson.ScalarText(v) is { Length: > 0 } s => [s],
        _ => [],
    };
}

/// <summary>A batch of records, oldest first, with the checkpoint to commit after the batch has been analysed and stored.</summary>
public sealed class SignalBatch(string sourceId, IReadOnlyList<LogRecord> records, object? checkpoint)
{
    public string SourceId { get; } = sourceId;
    public IReadOnlyList<LogRecord> Records { get; } = records;
    /// <summary>Opaque to everything but the source that produced it. Null for batches that move no checkpoint.</summary>
    public object? Checkpoint { get; } = checkpoint;
}

public enum SourceStatus { Unknown, Healthy, Unhealthy }

/// <summary>Shown on the Sources page.</summary>
public sealed record SourceHealth(
    string SourceId, string Kind, SourceStatus Status, string? Error, DateTimeOffset? LastSuccess, DateTimeOffset? UnhealthySince,
    IReadOnlyDictionary<string, string> Details);

/// <summary>A source of log records (operations.md 6.2). A project adds a kind by implementing this and registering it by kind.</summary>
public interface ISignalSource
{
    string Id { get; }
    string Kind { get; }                                                // "log_analytics", "log_files", "http_health", "world"
    Task StartAsync(SourceContext context, CancellationToken ct);       // validate settings, discovery
    IAsyncEnumerable<SignalBatch> ReadAsync(CancellationToken ct);      // batches of LogRecord, oldest first
    Task CommitAsync(SignalBatch batch, CancellationToken ct);          // save the checkpoint after analysis
    Task<SourceHealth> GetHealthAsync(CancellationToken ct);            // shown on the Sources page
}

/// <summary>Creates the sources of one kind. Registered in dependency injection; the host picks by <see cref="Kind"/>.</summary>
public interface ISignalSourceFactory
{
    string Kind { get; }
    ISignalSource Create(string sourceId);
}

public sealed class SourceConfigurationException(string message) : MonitorException(message);

public sealed record FileOffset(string SourceId, string HeadHash, string Path, long Offset, DateTimeOffset UpdatedAt);

public sealed record DiscoveredResource(string SourceId, string ResourceId, DateTimeOffset RefreshedAt);

/// <summary>
/// The state tables of the sources: <c>source_offsets</c>, <c>source_watermarks</c>, <c>seen_records</c>, <c>discovered_resources</c>
/// (operations.md 10.2). The in-memory implementation serves tests and <c>analyse</c>; the SQLite store implements it later.
/// </summary>
public interface ISourceStateStore
{
    Task<IReadOnlyList<FileOffset>> GetOffsetsAsync(string sourceId, CancellationToken ct);
    /// <summary>Atomically removes the entries with the given head hashes and upserts the new ones.</summary>
    Task SaveOffsetsAsync(string sourceId, IReadOnlyCollection<string> removeHeadHashes, IReadOnlyCollection<FileOffset> upserts, CancellationToken ct);

    Task<string?> GetWatermarkAsync(string sourceId, CancellationToken ct);
    Task SetWatermarkAsync(string sourceId, string watermark, CancellationToken ct);

    /// <summary>Records a record id; false when it was already seen (the duplicate is dropped).</summary>
    Task<bool> TryMarkSeenAsync(string sourceId, string recordId, DateTimeOffset seenAt, CancellationToken ct);
    Task PurgeSeenAsync(DateTimeOffset olderThan, CancellationToken ct);

    Task<DiscoveredResource?> GetDiscoveredAsync(string sourceId, CancellationToken ct);
    Task SetDiscoveredAsync(DiscoveredResource resource, CancellationToken ct);
}

public sealed class InMemorySourceStateStore : ISourceStateStore
{
    private readonly object _lock = new();
    private readonly Dictionary<(string, string), FileOffset> _offsets = [];
    private readonly Dictionary<string, string> _watermarks = [];
    private readonly Dictionary<(string, string), DateTimeOffset> _seen = [];
    private readonly Dictionary<string, DiscoveredResource> _discovered = [];

    public Task<IReadOnlyList<FileOffset>> GetOffsetsAsync(string sourceId, CancellationToken ct)
    {
        lock (_lock) return Task.FromResult<IReadOnlyList<FileOffset>>(_offsets.Where(kv => kv.Key.Item1 == sourceId).Select(kv => kv.Value).ToList());
    }

    public Task SaveOffsetsAsync(string sourceId, IReadOnlyCollection<string> removeHeadHashes, IReadOnlyCollection<FileOffset> upserts, CancellationToken ct)
    {
        lock (_lock)
        {
            foreach (var h in removeHeadHashes) _offsets.Remove((sourceId, h));
            foreach (var o in upserts) _offsets[(sourceId, o.HeadHash)] = o;
        }
        return Task.CompletedTask;
    }

    public Task<string?> GetWatermarkAsync(string sourceId, CancellationToken ct)
    {
        lock (_lock) return Task.FromResult(_watermarks.GetValueOrDefault(sourceId));
    }

    public Task SetWatermarkAsync(string sourceId, string watermark, CancellationToken ct)
    {
        lock (_lock) _watermarks[sourceId] = watermark;
        return Task.CompletedTask;
    }

    public Task<bool> TryMarkSeenAsync(string sourceId, string recordId, DateTimeOffset seenAt, CancellationToken ct)
    {
        lock (_lock) return Task.FromResult(_seen.TryAdd((sourceId, recordId), seenAt));
    }

    public Task PurgeSeenAsync(DateTimeOffset olderThan, CancellationToken ct)
    {
        lock (_lock)
            foreach (var key in _seen.Where(kv => kv.Value < olderThan).Select(kv => kv.Key).ToList()) _seen.Remove(key);
        return Task.CompletedTask;
    }

    public Task<DiscoveredResource?> GetDiscoveredAsync(string sourceId, CancellationToken ct)
    {
        lock (_lock) return Task.FromResult(_discovered.GetValueOrDefault(sourceId));
    }

    public Task SetDiscoveredAsync(DiscoveredResource resource, CancellationToken ct)
    {
        lock (_lock) _discovered[resource.SourceId] = resource;
        return Task.CompletedTask;
    }

    public int SeenCount { get { lock (_lock) return _seen.Count; } }
}
