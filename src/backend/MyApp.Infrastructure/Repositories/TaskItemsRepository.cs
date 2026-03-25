using MyApp.Domain.Constants;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using MyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for task items.
/// <userstory ref="US-TASK-01, US-TASK-02, US-TASK-03" />
/// </summary>
public class TaskItemsRepository(
    ILogger<TaskItemsRepository> logger,
    MyAppDbContext context
) : ITaskItemsRepository
{
    /// <inheritdoc />
    public async Task<(List<TaskItem> Items, int TotalCount)> GetByOrganizationIdAsync(
        Guid organizationId,
        string? statusFilter,
        Guid? assignedToUserId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching tasks for org {OrgId} (status={Status}, assigned={Assigned}, search={Search}, page={Page})",
            organizationId, statusFilter, assignedToUserId, search, page);

        var query = context.TaskItems
            .Where(t => t.OrganizationId == organizationId);

        if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<TaskItemStatus>(statusFilter, true, out var status))
            query = query.Where(t => t.Status == status);

        if (assignedToUserId.HasValue)
            query = query.Where(t => t.AssignedToUserId == assignedToUserId.Value);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(t => t.Title.Contains(search) || (t.Description != null && t.Description.Contains(search)));

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(t => t.AssignedToUser)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<TaskItem?> GetByIdAsync(
        Guid organizationId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching task {TaskId} in org {OrgId}", taskId, organizationId);

        return await context.TaskItems
            .Include(t => t.AssignedToUser)
            .FirstOrDefaultAsync(t => t.Id == taskId && t.OrganizationId == organizationId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Adding task '{Title}' in org {OrgId}", task.Title, task.OrganizationId);
        await context.TaskItems.AddAsync(task, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Updating task {TaskId}", task.Id);
        context.TaskItems.Update(task);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Deleting task {TaskId}", task.Id);
        context.TaskItems.Remove(task);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<TaskItem>> GetOverdueAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await context.TaskItems
            .Where(t => t.DueDate.HasValue
                        && t.DueDate.Value < today
                        && t.Status != TaskItemStatus.Done
                        && t.Status != TaskItemStatus.Cancelled)
            .Include(t => t.AssignedToUser)
            .Include(t => t.Organization)
            .ToListAsync(cancellationToken);
    }
}
