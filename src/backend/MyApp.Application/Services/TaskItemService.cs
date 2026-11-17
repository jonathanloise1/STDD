using System.ComponentModel.DataAnnotations;
using MyApp.Application.DTOs.Tasks;
using MyApp.Application.Exceptions;
using MyApp.Application.Interfaces;
using MyApp.Application.Logging;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace MyApp.Application.Services;

/// <summary>
/// Business logic for task items.
/// </summary>
// US-TASK-01, US-TASK-02, US-TASK-03, US-TASK-04, US-TASK-05
public class TaskItemService(
    ILogger<TaskItemService> logger,
    ITaskItemsRepository taskRepo,
    IOrganizationsRepository orgRepo
) : ITaskItemService
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    /// <inheritdoc />
    // US-TASK-05
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
        await MembershipChecks.EnsureActiveMemberAsync(orgRepo, authenticatedAadId, organizationId, cancellationToken);

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
    // US-TASK-05
    public async Task<TaskItemDto> GetByIdAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        await MembershipChecks.EnsureActiveMemberAsync(orgRepo, authenticatedAadId, organizationId, cancellationToken);

        var task = await taskRepo.GetByIdAsync(organizationId, taskId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task {taskId} not found.");

        return MapToDto(task);
    }

    /// <inheritdoc />
    // US-TASK-01
    public async Task<TaskItemDto> CreateAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var membership = await MembershipChecks.EnsureActiveMemberAsync(orgRepo, authenticatedAadId, organizationId, cancellationToken);

        if (membership.Role == OrganizationRole.Viewer)
        {
            logger.TaskCreateNotAllowed(membership.Role, authenticatedAadId, organizationId);
            throw new AccessLostException(TasksLog.Keys.TaskCreateNotAllowed, OutcomeMessages.RoleNotAllowed);
        }

        var title = ValidateTitle(request.Title);
        ValidateDescription(request.Description);

        var task = new TaskItem
        {
            OrganizationId = organizationId,
            Title = title,
            Description = request.Description,
            AssignedToUserId = request.AssignedToUserId,
            DueDate = request.DueDate
        };

        await taskRepo.AddAsync(task, cancellationToken);
        logger.LogInformation("Task {TaskId} created in org {OrgId}", task.Id, organizationId);

        // Re-fetch to include navigation properties
        var created = await taskRepo.GetByIdAsync(organizationId, task.Id, cancellationToken);
        return MapToDto(created!);
    }

    /// <inheritdoc />
    // US-TASK-02, US-TASK-03
    public async Task<TaskItemDto> UpdateAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        Guid taskId,
        UpdateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var membership = await MembershipChecks.EnsureActiveMemberAsync(orgRepo, authenticatedAadId, organizationId, cancellationToken);

        if (membership.Role == OrganizationRole.Viewer)
        {
            logger.TaskEditNotAllowed(membership.Role, authenticatedAadId, organizationId);
            throw new AccessLostException(TasksLog.Keys.TaskEditNotAllowed, OutcomeMessages.RoleNotAllowed);
        }

        var task = await taskRepo.GetByIdAsync(organizationId, taskId, cancellationToken);
        if (task is null)
        {
            // The "Change status" action sends the status alone; every other update is an edit
            if (IsStatusChangeOnly(request))
            {
                logger.TaskStatusNotFound(taskId, organizationId);
                throw new ResourceNotFoundException(TasksLog.Keys.TaskStatusNotFound, OutcomeMessages.TaskNotFound);
            }

            logger.TaskEditNotFound(taskId, organizationId);
            throw new ResourceNotFoundException(TasksLog.Keys.TaskEditNotFound, OutcomeMessages.TaskNotFound);
        }

        // Only what was sent changes; everything else is kept
        if (request.Title is not null)
            task.Title = ValidateTitle(request.Title);

        if (request.Description is not null)
        {
            ValidateDescription(request.Description);
            task.Description = request.Description.Length == 0 ? null : request.Description;
        }

        if (request.AssignedToUserId.HasValue)
            task.AssignedToUserId = request.AssignedToUserId;

        if (request.DueDate.HasValue)
            task.DueDate = request.DueDate;
        else if (request.ClearDueDate)
            task.DueDate = null;

        if (request.Status.HasValue)
            task.Status = request.Status.Value;

        await taskRepo.UpdateAsync(task, cancellationToken);
        logger.LogInformation("Task {TaskId} updated in org {OrgId}", taskId, organizationId);

        var updated = await taskRepo.GetByIdAsync(organizationId, taskId, cancellationToken);
        return MapToDto(updated!);
    }

    /// <inheritdoc />
    // US-TASK-04
    public async Task DeleteAsync(
        Guid authenticatedAadId,
        Guid organizationId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var membership = await MembershipChecks.EnsureActiveMemberAsync(orgRepo, authenticatedAadId, organizationId, cancellationToken);

        // No story covers a Viewer sending a delete (the button is not offered): refused as a rejected request
        if (membership.Role == OrganizationRole.Viewer)
            throw new UnauthorizedAccessException(OutcomeMessages.RoleNotAllowed);

        var task = await taskRepo.GetByIdAsync(organizationId, taskId, cancellationToken);
        if (task is null)
        {
            logger.TaskDeleteNotFound(taskId, organizationId);
            throw new ResourceNotFoundException(TasksLog.Keys.TaskDeleteNotFound, OutcomeMessages.TaskNotFound);
        }

        await taskRepo.DeleteAsync(task, cancellationToken);
        logger.LogInformation("Task {TaskId} deleted from org {OrgId}", taskId, organizationId);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static bool IsStatusChangeOnly(UpdateTaskRequest request) =>
        request is { Status: not null, Title: null, Description: null, AssignedToUserId: null, DueDate: null, ClearDueDate: false };

    private static string ValidateTitle(string? title)
    {
        var trimmed = title?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ValidationException(new ValidationResult("Enter a title.", ["title"]), null, title);
        if (trimmed.Length > TitleMaxLength)
            throw new ValidationException(new ValidationResult($"The title can have at most {TitleMaxLength} characters.", ["title"]), null, title);
        return trimmed;
    }

    private static void ValidateDescription(string? description)
    {
        if (description is { Length: > DescriptionMaxLength })
            throw new ValidationException(new ValidationResult($"The description can have at most {DescriptionMaxLength} characters.", ["description"]), null, description);
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
