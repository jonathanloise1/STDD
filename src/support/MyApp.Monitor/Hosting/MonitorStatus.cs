using System.Collections.Concurrent;
using MyApp.Monitor.Sources;

namespace MyApp.Monitor.Hosting;

/// <summary>
/// What the running monitor knows about itself and the store does not: the live health of its sources, the state of the pack and
/// the investigator. The Sources page shows it next to the offsets and watermarks of the store.
/// </summary>
public sealed class MonitorStatus(TimeProvider time)
{
    private readonly ConcurrentDictionary<string, ISignalSource> _sources = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SourceHealth> _startFailures = new(StringComparer.Ordinal);

    public DateTimeOffset StartedAt { get; } = time.GetUtcNow();
    public string? PackPath { get; set; }
    public string? PackError { get; private set; }
    public DateTimeOffset? PackErrorAt { get; private set; }
    public DateTimeOffset? PackLoadedAt { get; private set; }
    public string? StorePath { get; set; }
    public int StoreSchemaVersion { get; set; }
    public bool Watching { get; set; }

    public void Register(ISignalSource source)
    {
        _sources[source.Id] = source;
    }

    /// <summary>The source started: what is shown from now on is its own health.</summary>
    public void StartSucceeded(string id) => _startFailures.TryRemove(id, out _);

    public void MarkStartFailure(string id, string kind, string error, DateTimeOffset? since) =>
        _startFailures[id] = new SourceHealth(id, kind, SourceStatus.Unhealthy, error, null, since ?? time.GetUtcNow(), new Dictionary<string, string>());

    public void PackLoaded() { PackError = null; PackErrorAt = null; PackLoadedAt = time.GetUtcNow(); }
    public void PackRefused(string error) { PackError = error; PackErrorAt = time.GetUtcNow(); }

    public async Task<IReadOnlyList<SourceHealth>> HealthAsync(CancellationToken ct)
    {
        var list = new List<SourceHealth>();
        foreach (var source in _sources.Values.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            if (_startFailures.TryGetValue(source.Id, out var failure)) { list.Add(failure); continue; }   // it has not started: say why
            try { list.Add(await source.GetHealthAsync(ct).ConfigureAwait(false)); }
            catch (Exception ex) { list.Add(new SourceHealth(source.Id, source.Kind, SourceStatus.Unhealthy, ex.Message, null, null, new Dictionary<string, string>())); }
        }
        foreach (var failure in _startFailures.Values.Where(f => list.All(l => l.SourceId != f.SourceId))) list.Add(failure);
        return list.OrderBy(h => h.SourceId, StringComparer.Ordinal).ToList();
    }
}
