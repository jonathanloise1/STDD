using MyApp.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Jobs;

/// <summary>
/// Example background job: logs overdue tasks periodically.
/// Demonstrates the pattern for scheduled background services.
/// In a real application, this could send email notifications.
/// </summary>
public class OverdueTaskNotificationJob(
    ILogger<OverdueTaskNotificationJob> logger,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OverdueTaskNotificationJob started (interval: {Interval})", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOverdueTasksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking overdue tasks");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CheckOverdueTasksAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskItemsRepository>();

        var overdueTasks = await taskRepo.GetOverdueAsync(ct);

        if (overdueTasks.Count == 0)
        {
            logger.LogDebug("No overdue tasks found.");
            return;
        }

        logger.LogWarning("Found {Count} overdue task(s):", overdueTasks.Count);
        foreach (var task in overdueTasks)
        {
            logger.LogWarning("  - Task '{Title}' (due {DueDate}) in org '{OrgName}', assigned to {Assignee}",
                task.Title,
                task.DueDate,
                task.Organization?.Name ?? "?",
                task.AssignedToUser?.GetFullName() ?? "unassigned");
        }
    }
}
