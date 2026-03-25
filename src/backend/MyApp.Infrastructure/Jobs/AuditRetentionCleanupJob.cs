using MyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Jobs;

/// <summary>
/// Background job that runs weekly and deletes audit logs older than the retention period (365 days).
/// </summary>
public class AuditRetentionCleanupJob(
    ILogger<AuditRetentionCleanupJob> logger,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private const int RetentionDays = 365;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AuditRetentionCleanupJob failed");
            }

            // Run once per week
            await Task.Delay(TimeSpan.FromDays(7), stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MyAppDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        var deleted = await context.AuditLogs
            .Where(a => a.Timestamp < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            logger.LogInformation("Deleted {Count} audit logs older than {Days} days", deleted, RetentionDays);
    }
}
