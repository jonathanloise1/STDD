using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Store;

namespace MyApp.Monitor.Hosting;

public sealed class MonitorHostOptions
{
    /// <summary>How often the host checks the pack, flushes suppression summaries, runs the digest and the daily retention.</summary>
    public TimeSpan MaintenanceInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>The pack path to watch, or null (a pack built locally is not watched).</summary>
    public string? WatchPackPath { get; set; }

    /// <summary>Wait for deliveries still in flight at shutdown.</summary>
    public TimeSpan ShutdownGrace { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan SourceRestartInitial { get; set; } = PollingSourceBase.InitialBackoff;
    public TimeSpan SourceRestartMax { get; set; } = PollingSourceBase.MaxBackoff;
    public TimeSpan SourceFailingAfter { get; set; } = PollingSourceBase.FailingAfter;
}

/// <summary>
/// The running monitor (operations.md 14.3, 6.2): one task per source, each batch analysed, written and then committed; a failing source,
/// notifier or investigator never stops the others. Stopping lets the batch in flight finish and its checkpoint be saved.
/// </summary>
public sealed class MonitorHost(
    RunContextHolder holder, RunContextFactory contexts, IncidentProcessor processor, SuppressionService suppression, DigestService digest,
    NotificationDispatcher notifier, DashboardAddress address, IIncidentStore store, ISourceStateStore state, IEnumerable<ISignalSourceFactory> factories, MonitorStatus status,
    MonitorHostOptions options, MonitorOptions monitorOptions, TimeProvider time, ILogger<MonitorHost> logger) : BackgroundService
{
    private readonly Dictionary<string, ISignalSourceFactory> _factories = factories.ToDictionary(f => f.Kind, StringComparer.Ordinal);
    private DateOnly _retentionDay = DateOnly.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await address.ReadyAsync.WaitAsync(stoppingToken).ConfigureAwait(false);
        var ctx = holder.Current;
        suppression.Configure(ctx.Profile.Suppression);
        var tasks = new List<Task> { Task.Run(() => MaintenanceAsync(stoppingToken), CancellationToken.None) };
        foreach (var source in ctx.Profile.Sources.Where(s => ProfileLoader.SignalKinds.Contains(s.Kind) && s.Kind != "world"))
        {
            var captured = source;
            tasks.Add(Task.Run(() => RunSourceAsync(captured, stoppingToken), CancellationToken.None));
        }
        logger.LogInformation("Monitor started: profile {Profile}, environment {Environment}, {Sources} source(s), pack {Pack}",
            ctx.Profile.Name, ctx.Runtime.Environment, tasks.Count - 1, ctx.Pack.Version);
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await suppression.FlushEndedAsync(time.GetUtcNow(), CancellationToken.None, all: true).ConfigureAwait(false);
            await notifier.DrainAsync(options.ShutdownGrace).ConfigureAwait(false);
        }
        catch (Exception ex) { logger.LogError(ex, "Shutdown housekeeping failed"); }
    }

    // ---- one source ----

    private async Task RunSourceAsync(SourceSettings settings, CancellationToken ct)
    {
        if (!_factories.TryGetValue(settings.Kind, out var factory))
        {
            status.MarkStartFailure(settings.Id, settings.Kind, $"no source of kind '{settings.Kind}' is registered", null);
            logger.LogError("Source {Source}: no source of kind {Kind} is registered", settings.Id, settings.Kind);
            return;
        }

        var backoff = options.SourceRestartInitial;
        DateTimeOffset? failingSince = null;
        var raised = false;
        while (!ct.IsCancellationRequested)
        {
            var source = factory.Create(settings.Id);
            status.Register(source);
            try
            {
                var context = new SourceContext(
                    settings.Id, settings.Kind, holder.Current.Runtime.Environment, settings.Settings, monitorOptions.ResolveContentRoot(), state, time, logger);
                await source.StartAsync(context, ct).ConfigureAwait(false);
                status.StartSucceeded(settings.Id);
                failingSince = null;
                raised = false;
                backoff = options.SourceRestartInitial;
                await foreach (var batch in source.ReadAsync(ct).ConfigureAwait(false))
                    await HandleBatchAsync(source, batch).ConfigureAwait(false);
                return;   // the source ended on its own or the monitor is stopping
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                var now = time.GetUtcNow();
                failingSince ??= now;
                status.MarkStartFailure(settings.Id, settings.Kind, ex.Message, failingSince);
                logger.LogWarning("Source {Source} stopped: {Error}; retrying in {Delay}", settings.Id, ex.Message, backoff);
                if (!raised && now - failingSince >= options.SourceFailingAfter)
                {
                    raised = true;
                    await HandleBatchAsync(source, new SignalBatch(settings.Id, [FailingRecord(settings, now)], null)).ConfigureAwait(false);
                }
                try { await Task.Delay(backoff, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, options.SourceRestartMax.Ticks));
            }
        }
    }

    private LogRecord FailingRecord(SourceSettings settings, DateTimeOffset now) => LogRecord.Create(
        settings.Id, holder.Current.Runtime.Environment, now, RecordLevel.Error, "MyApp.Monitor.Sources",
        $"Source {settings.Id} has been unhealthy for over 15 minutes",
        eventName: PollingSourceBase.FailingEventName,
        messageTemplate: "Source {SourceId} has been unhealthy for over 15 minutes",
        properties: new Dictionary<string, string> { ["SourceId"] = settings.Id });

    /// <summary>Analyse, write, then commit the checkpoint (at-least-once delivery, 6.2). The batch in flight is finished even when the monitor is stopping.</summary>
    private async Task HandleBatchAsync(ISignalSource source, SignalBatch batch)
    {
        try
        {
            var ctx = holder.Current;   // one pack for the whole batch: a replacement applies between batches
            var results = await ctx.Pipeline.ProcessAsync(batch, CancellationToken.None).ConfigureAwait(false);
            foreach (var result in results) await processor.ProcessAsync(ctx, result, CancellationToken.None).ConfigureAwait(false);
            await source.CommitAsync(batch, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // the checkpoint stays where it was: the records are read again after a restart
            logger.LogError(ex, "Source {Source}: a batch of {Count} record(s) could not be written; its checkpoint was not saved", batch.SourceId, batch.Records.Count);
        }
    }

    // ---- maintenance ----

    private async Task MaintenanceAsync(CancellationToken ct)
    {
        PackWatcher? watcher = options.WatchPackPath is null ? null
            : new PackWatcher(options.WatchPackPath, holder, contexts, status, logger);
        status.Watching = watcher is not null;
        using var timer = new PeriodicTimer(options.MaintenanceInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                await MaintenanceTickAsync(watcher, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* stopping */ }
    }

    public async Task MaintenanceTickAsync(PackWatcher? watcher, CancellationToken ct)
    {
        try
        {
            if (watcher?.Check() == true) suppression.Configure(holder.Current.Profile.Suppression);
            var now = time.GetUtcNow();
            await suppression.FlushEndedAsync(now, ct).ConfigureAwait(false);
            await digest.RunDueAsync(holder.Current.Notifications, ct).ConfigureAwait(false);
            await RetentionIfDueAsync(now, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogError(ex, "Maintenance failed"); }
    }

    /// <summary>Retention runs once a day at 03:00 local time (operations.md 10.2).</summary>
    private async Task RetentionIfDueAsync(DateTimeOffset now, CancellationToken ct)
    {
        var local = TimeZoneInfo.ConvertTime(now, time.LocalTimeZone);
        var today = DateOnly.FromDateTime(local.Date);
        if (_retentionDay == today || local.Hour < 3) return;
        _retentionDay = today;
        var result = await store.PurgeAsync(now, holder.Current.Profile.Store.RetentionDays, ct).ConfigureAwait(false);
        logger.LogInformation("Retention: {Incidents} incident(s), {Events} event(s), {Notifications} notification(s), {Audit} audit row(s), {Counters} counter row(s), {Seen} seen record(s) removed",
            result.Incidents, result.Events, result.Notifications, result.ToolAudit, result.Counters, result.SeenRecords);
    }
}
