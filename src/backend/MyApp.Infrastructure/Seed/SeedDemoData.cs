using MyApp.Domain.Entities;
using MyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Seed;

/// <summary>
/// Seeds the tasks and the audit history of <see cref="SeedDataSet"/>. Runs after
/// <see cref="SeedLocalDevelopment"/>. Idempotent: what exists is kept.
/// </summary>
public static class SeedDemoData
{
    public static async Task SeedAsync(MyAppDbContext context, ILogger? logger, DateTime now, CancellationToken ct = default)
    {
        var userIds = await SeedLocalDevelopment.UserIdsAsync(context, ct);
        if (!userIds.ContainsKey(SeedDataSet.DevUser))
        {
            logger?.LogInformation("Demo data not seeded: the dev user does not exist yet.");
            return;
        }

        var existingTasks = await context.TaskItems.Select(t => new { t.Id, t.OrganizationId, t.Title }).ToListAsync(ct);
        foreach (var t in SeedDataSet.Tasks(now))
        {
            if (existingTasks.Any(e => e.Id == t.Id || (e.OrganizationId == t.OrganizationId && e.Title == t.Title)))
                continue;

            context.TaskItems.Add(new TaskItem
            {
                Id = t.Id,
                OrganizationId = t.OrganizationId,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                AssignedToUserId = t.AssignedToUserAadId is { } aad ? userIds[aad] : null,
                DueDate = t.DueDate,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            });
        }

        await context.SaveChangesAsync(ct);

        // Nothing is audited while nobody is signed in, so the history is written here
        var existingAudit = (await context.AuditLogs.Select(a => a.Id).ToListAsync(ct)).ToHashSet();
        foreach (var a in SeedDataSet.AuditEntries(now).Where(a => !existingAudit.Contains(a.Id)))
        {
            context.AuditLogs.Add(new AuditLog
            {
                Id = a.Id,
                OrganizationId = a.OrganizationId,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Action = a.Action,
                UserId = userIds[a.UserAadId],
                UserDisplayName = a.UserDisplayName,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                ChangedProperties = a.ChangedProperties,
                Timestamp = a.Timestamp,
                CorrelationId = a.CorrelationId
            });
        }

        await context.SaveChangesAsync(ct);
        logger?.LogInformation("Demo data is in place.");
    }
}
