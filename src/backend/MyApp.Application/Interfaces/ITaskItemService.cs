using MyApp.Application.DTOs.Tasks;

namespace MyApp.Application.Interfaces;

/// <summary>
/// Service interface for task item business logic.
/// <userstory ref="US-TASK-01, US-TASK-02, US-TASK-03" />
/// </summary>
public interface ITaskItemService
{
    /// <summary>Returns paginated tasks for an organization.</summary>
    Task<TaskItemPagedResult> GetByOrganizationAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        string? statusFilter,
        Guid? assignedToUserId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a single task by ID.</summary>
    Task<TaskItemDto> GetByIdAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        Guid taskId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a new task.</summary>
    Task<TaskItemDto> CreateAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Updates an existing task.</summary>
    Task<TaskItemDto> UpdateAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        Guid taskId,
        UpdateTaskRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a task.</summary>
    Task DeleteAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        Guid taskId,
        CancellationToken cancellationToken = default);
}
