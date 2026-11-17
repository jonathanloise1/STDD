using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace MyApp.Monitor.Sources;

public sealed record PollResult(IReadOnlyList<SignalBatch> Batches, TimeSpan Delay);

/// <summary>
/// Shared behaviour of polling sources (operations.md 6.2): a failing source keeps its checkpoint, retries with backoff
/// (10 s doubling to 5 min), reports <c>Unhealthy</c>, and after 15 minutes raises <c>monitor.source.failing</c> (Error).
/// </summary>
public abstract class PollingSourceBase(string id, string kind) : ISignalSource
{
    public static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan FailingAfter = TimeSpan.FromMinutes(15);
    public const string FailingEventName = "monitor.source.failing";

    private TimeSpan _backoff = TimeSpan.Zero;
    private DateTimeOffset? _lastSuccess;
    private DateTimeOffset? _unhealthySince;
    private string? _error;
    private bool _failingRaised;

    public string Id { get; } = id;
    public string Kind { get; } = kind;
    protected SourceContext Context { get; private set; } = null!;
    protected TimeProvider Time => Context.Time;

    /// <summary>Delay between polls when all goes well.</summary>
    protected abstract TimeSpan PollInterval { get; }

    public async Task StartAsync(SourceContext context, CancellationToken ct)
    {
        Context = context;
        await OnStartAsync(ct).ConfigureAwait(false);
    }

    protected virtual Task OnStartAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>One poll. Throw to report a failure; return the batches found. <paramref name="immediate"/> asks for the next poll without waiting.</summary>
    protected abstract Task<IReadOnlyList<SignalBatch>> PollAsync(CancellationToken ct);

    protected virtual bool ContinueImmediately => false;

    public abstract Task CommitAsync(SignalBatch batch, CancellationToken ct);

    protected virtual IReadOnlyDictionary<string, string> HealthDetails() => new Dictionary<string, string>();

    public async Task<PollResult> RunOnceAsync(CancellationToken ct)
    {
        var now = Time.GetUtcNow();
        try
        {
            var batches = await PollAsync(ct).ConfigureAwait(false);
            _lastSuccess = now;
            _unhealthySince = null;
            _error = null;
            _failingRaised = false;
            _backoff = TimeSpan.Zero;
            return new PollResult(batches, ContinueImmediately ? TimeSpan.Zero : PollInterval);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (SourceConfigurationException ex)
        {
            return Failed(now, ex.Message, configuration: true);
        }
        catch (Exception ex)
        {
            return Failed(now, ex.Message, configuration: false);
        }
    }

    private PollResult Failed(DateTimeOffset now, string message, bool configuration)
    {
        _error = message;
        _unhealthySince ??= now;
        _backoff = _backoff == TimeSpan.Zero ? InitialBackoff : TimeSpan.FromTicks(Math.Min(_backoff.Ticks * 2, MaxBackoff.Ticks));
        Context.Logger?.LogWarning("Source {Source} failed: {Error}", Id, message);
        _ = configuration;

        var batches = new List<SignalBatch>();
        if (!_failingRaised && now - _unhealthySince >= FailingAfter)
        {
            _failingRaised = true;
            batches.Add(new SignalBatch(Id, [FailingRecord(now)], null));
        }
        return new PollResult(batches, _backoff);
    }

    private LogRecord FailingRecord(DateTimeOffset now) => LogRecord.Create(
        Id, Context.Environment, now, RecordLevel.Error, "MyApp.Monitor.Sources",
        $"Source {Id} has been unhealthy for over 15 minutes",
        eventName: FailingEventName,
        messageTemplate: "Source {SourceId} has been unhealthy for over 15 minutes",
        properties: new Dictionary<string, string> { ["SourceId"] = Id });

    public async IAsyncEnumerable<SignalBatch> ReadAsync([EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await RunOnceAsync(ct).ConfigureAwait(false);
            foreach (var batch in result.Batches) yield return batch;
            if (result.Delay > TimeSpan.Zero)
            {
                try { await Task.Delay(result.Delay, Time, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { yield break; }
            }
        }
    }

    public Task<SourceHealth> GetHealthAsync(CancellationToken ct)
    {
        var status = _error is not null ? SourceStatus.Unhealthy : _lastSuccess is not null ? SourceStatus.Healthy : SourceStatus.Unknown;
        return Task.FromResult(new SourceHealth(Id, Kind, status, _error, _lastSuccess, _unhealthySince, HealthDetails()));
    }
}
