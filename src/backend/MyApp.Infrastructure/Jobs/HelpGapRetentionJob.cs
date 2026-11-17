using MyApp.Application.Help;
using MyApp.Domain.Repositories;
using MyApp.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApp.Application.Logging;

namespace MyApp.Infrastructure.Jobs;

/// <summary>
/// Background job that runs daily and deletes the documentation gaps older than
/// <c>MyApp:Help:Gaps:RetentionDays</c> (manual.md 7.4).
/// </summary>
// US-HELP-07
public class HelpGapRetentionJob(
    ILogger<HelpGapRetentionJob> logger,
    IServiceScopeFactory scopeFactory,
    HelpSettings settings,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var summary = ExceptionSummary.From(ex);
                logger.PlatformJobFailed(ex, nameof(HelpGapRetentionJob), summary.ExceptionType, summary.TopFrame);
            }

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IHelpGapsRepository>();

        var cutoff = HelpGapService.RetentionCutoff(timeProvider.GetUtcNow().UtcDateTime, settings.GapsRetentionDays);
        var deleted = await repository.DeleteOlderThanAsync(cutoff, ct);

        if (deleted > 0)
            logger.LogInformation("Deleted {Count} documentation gaps older than {Days} days", deleted, settings.GapsRetentionDays);
    }
}
