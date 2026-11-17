using MyApp.Application.DTOs.Tasks;
using MyApp.Application.Interfaces;
using MyApp.Domain.Constants;
using MyApp.Domain.Repositories;

namespace MyApp.Application.Services;

/// <summary>Task figures of the Dashboard page.</summary>
// US-DASH-01, US-DASH-02
public class DashboardService(
    ITaskItemsRepository taskRepo,
    IOrganizationsRepository orgRepo) : IDashboardService
{
    public const int RecentTasksCount = 5;

    /// <inheritdoc />
    // US-DASH-01
    public async Task<DashboardCountsDto> GetCountsAsync(Guid userAadId, Guid organizationId, CancellationToken ct = default)
    {
        await MembershipChecks.EnsureActiveMemberAsync(orgRepo, userAadId, organizationId, ct);

        var byStatus = await taskRepo.CountByStatusAsync(organizationId, ct);
        int Of(TaskItemStatus s) => byStatus.TryGetValue(s, out var n) ? n : 0;

        return new DashboardCountsDto
        {
            Total = byStatus.Values.Sum(),
            Todo = Of(TaskItemStatus.Todo),
            InProgress = Of(TaskItemStatus.InProgress),
            Done = Of(TaskItemStatus.Done),
            Cancelled = Of(TaskItemStatus.Cancelled)
        };
    }

    /// <inheritdoc />
    // US-DASH-02
    public async Task<IReadOnlyList<TaskItemDto>> GetRecentTasksAsync(Guid userAadId, Guid organizationId, CancellationToken ct = default)
    {
        await MembershipChecks.EnsureActiveMemberAsync(orgRepo, userAadId, organizationId, ct);

        var tasks = await taskRepo.GetRecentAsync(organizationId, RecentTasksCount, ct);
        return tasks.Select(t => new TaskItemDto
        {
            Id = t.Id,
            OrganizationId = t.OrganizationId,
            Title = t.Title,
            Description = t.Description,
            Status = t.Status.ToString(),
            AssignedToUserId = t.AssignedToUserId,
            AssignedToName = t.AssignedToUser?.GetFullName(),
            DueDate = t.DueDate,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();
    }
}
