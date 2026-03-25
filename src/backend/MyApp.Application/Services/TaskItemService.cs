using AutoMapper;
using MyApp.Application.DTOs.Tasks;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace MyApp.Application.Services;

/// <summary>
/// Business logic for task items.
/// <userstory ref="US-TASK-01, US-TASK-02, US-TASK-03" />
/// </summary>
public class TaskItemService(
    ILogger<TaskItemService> logger,
    ITaskItemsRepository taskRepo,
    IOrganizationsRepository orgRepo,
    IMapper mapper
) : ITaskItemService
{
    /// <inheritdoc />
    // US-TASK-02: List tasks with filters and pagination
    public async Task<TaskItemPagedResult> GetByOrganizationAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        string? statusFilter,
        Guid? assignedToUserId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        await EnsureMembershipAsync(authenticatedAadId, organizationId, cancellationToken);

        var (items, totalCount) = await taskRepo.GetByOrganizationIdAsync(
            organizationId, statusFilter, assignedToUserId, search, page, pageSize, cancellationToken);

        return new TaskItemPagedResult
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    // US-TASK-02: View task details
    public async Task<TaskItemDto> GetByIdAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        await EnsureMembershipAsync(authenticatedAadId, organizationId, cancellationToken);

        var task = await taskRepo.GetByIdAsync(organizationId, taskId, cancellationToken)
            ?? throw new InvalidOperationException($"Task {taskId} not found.");

        return MapToDto(task);
    }

    /// <inheritdoc />
    // US-TASK-01: Create a new task
    public async Task<TaskItemDto> CreateAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureMembershipAsync(authenticatedAadId, organizationId, cancellationToken);

        var task = new TaskItem
        {
            OrganizationId = organizationId,
            Title = request.Title,
            Description = request.Description,
            AssignedToUserId = request.AssignedToUserId,
            DueDate = request.DueDate
        };

        await taskRepo.AddAsync(task, cancellationToken);
        logger.LogInformation("Task '{Title}' created in org {OrgId}", task.Title, organizationId);

        // Re-fetch to include navigation properties
        var created = await taskRepo.GetByIdAsync(organizationId, task.Id, cancellationToken);
        return MapToDto(created!);
    }

    /// <inheritdoc />
    // US-TASK-03: Update task (title, description, status, assignment, due date)
    public async Task<TaskItemDto> UpdateAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        Guid taskId,
        UpdateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureMembershipAsync(authenticatedAadId, organizationId, cancellationToken);

        var task = await taskRepo.GetByIdAsync(organizationId, taskId, cancellationToken)
            ?? throw new InvalidOperationException($"Task {taskId} not found.");

        task.Title = request.Title;
        task.Description = request.Description;
        task.AssignedToUserId = request.AssignedToUserId;
        task.DueDate = request.DueDate;

        if (request.Status.HasValue)
            task.Status = request.Status.Value;

        await taskRepo.UpdateAsync(task, cancellationToken);
        logger.LogInformation("Task {TaskId} updated in org {OrgId}", taskId, organizationId);

        var updated = await taskRepo.GetByIdAsync(organizationId, taskId, cancellationToken);
        return MapToDto(updated!);
    }

    /// <inheritdoc />
    // US-TASK-03: Delete a task
    public async Task DeleteAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        await EnsureMembershipAsync(authenticatedAadId, organizationId, cancellationToken);

        var task = await taskRepo.GetByIdAsync(organizationId, taskId, cancellationToken)
            ?? throw new InvalidOperationException($"Task {taskId} not found.");

        await taskRepo.DeleteAsync(task, cancellationToken);
        logger.LogInformation("Task {TaskId} deleted from org {OrgId}", taskId, organizationId);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private async Task EnsureMembershipAsync(Guid aadId, Guid orgId, CancellationToken ct)
    {
        var org = await orgRepo.GetByIdAsync(orgId, ct)
            ?? throw new InvalidOperationException($"Organization {orgId} not found.");

        var isMember = org.Users.Any(u => u.User != null && u.User.AadId == aadId
            && u.Status == OrganizationUserStatus.Active);

        if (!isMember)
            throw new UnauthorizedAccessException("User is not an active member of this organization.");
    }

    private static TaskItemDto MapToDto(TaskItem task) => new()
    {
        Id = task.Id,
        OrganizationId = task.OrganizationId,
        Title = task.Title,
        Description = task.Description,
        Status = task.Status.ToString(),
        AssignedToUserId = task.AssignedToUserId,
        AssignedToName = task.AssignedToUser?.GetFullName(),
        DueDate = task.DueDate,
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt
    };
}
