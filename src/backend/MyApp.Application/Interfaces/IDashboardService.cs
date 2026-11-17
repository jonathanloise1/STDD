using MyApp.Application.DTOs.Tasks;

namespace MyApp.Application.Interfaces;

/// <summary>Task figures of the Dashboard page.</summary>
public interface IDashboardService
{
    /// <summary>Counts over every task of the organization.</summary>
    // US-DASH-01
    Task<DashboardCountsDto> GetCountsAsync(Guid userAadId, Guid organizationId, CancellationToken ct = default);

    /// <summary>The five most recently created tasks, whatever their status.</summary>
    // US-DASH-02
    Task<IReadOnlyList<TaskItemDto>> GetRecentTasksAsync(Guid userAadId, Guid organizationId, CancellationToken ct = default);
}
