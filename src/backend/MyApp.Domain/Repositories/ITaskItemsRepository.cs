using MyApp.Domain.Constants;
using MyApp.Domain.Entities;

namespace MyApp.Domain.Repositories;

/// <summary>
/// Repository interface for task items.
/// </summary>
// US-TASK-01, US-TASK-02, US-TASK-03, US-TASK-04, US-TASK-05, US-DASH-01, US-DASH-02
public interface ITaskItemsRepository
{
    /// <summary>Returns paginated task items for an organization with optional filters.</summary>
    Task<(List<TaskItem> Items, int TotalCount)> GetByOrganizationIdAsync(
        Guid organizationId,
        string? statusFilter,
        Guid? assignedToUserId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a single task item by ID within an organization.</summary>
    Task<TaskItem?> GetByIdAsync(
        Guid organizationId,
        Guid taskId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a new task item.</summary>
    Task AddAsync(TaskItem task, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing task item.</summary>
    Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default);

    /// <summary>Deletes a task item.</summary>
    Task DeleteAsync(TaskItem task, CancellationToken cancellationToken = default);

    /// <summary>Counts every task of an organization by status; statuses without tasks are absent.</summary>
    Task<Dictionary<TaskItemStatus, int>> CountByStatusAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the most recently created tasks of an organization, newest first, whatever their status.</summary>
    Task<List<TaskItem>> GetRecentAsync(
        Guid organizationId,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>Returns overdue tasks (past DueDate, not Done/Cancelled).</summary>
    Task<List<TaskItem>> GetOverdueAsync(CancellationToken cancellationToken = default);
}
