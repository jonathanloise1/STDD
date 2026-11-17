using MyApp.Application.DTOs.Tasks;
using MyApp.Application.Interfaces;
using MyApp.Application.Logging;
using MyApp.WebApi.Filters;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.WebApi.Controllers;

/// <summary>
/// CRUD endpoints for task items within an organization. Failures become problem documents in
/// <c>ExceptionHandlingMiddleware</c>; the services raise the keyed outcomes.
/// </summary>
[Route("api/organizations/{organizationId}/tasks")]
public class TasksController(
    ILogger<TasksController> logger,
    ITaskItemService taskService) : AuthenticatedControllerBase
{
    /// <summary>List tasks with optional filters and pagination.</summary>
    // US-TASK-05
    [HttpGet]
    [ServiceUnavailable(TasksLog.Keys.TaskListUnavailable)]
    public async Task<IActionResult> List(
        Guid organizationId,
        [FromQuery] string? status = null,
        [FromQuery] Guid? assignedTo = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId
        });

        logger.LogDebug("Listing tasks (status={Status}, assignedTo={AssignedTo}, page={Page})",
            status, assignedTo, page);

        var result = await taskService.GetByOrganizationAsync(
            AuthenticatedUserId, organizationId, status, assignedTo, search, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Get a single task by ID.</summary>
    // US-TASK-05
    [HttpGet("{taskId}")]
    public async Task<IActionResult> GetById(
        Guid organizationId,
        Guid taskId,
        CancellationToken ct = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId,
            ["TaskId"] = taskId
        });

        var result = await taskService.GetByIdAsync(AuthenticatedUserId, organizationId, taskId, ct);
        return Ok(result);
    }

    /// <summary>Create a new task.</summary>
    // US-TASK-01
    [HttpPost]
    [ServiceUnavailable(TasksLog.Keys.TaskCreateUnavailable)]
    public async Task<IActionResult> Create(
        Guid organizationId,
        [FromBody] CreateTaskRequest request,
        CancellationToken ct = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId
        });

        logger.LogDebug("Creating a task");

        var result = await taskService.CreateAsync(AuthenticatedUserId, organizationId, request, ct);
        return CreatedAtAction(nameof(GetById), new { organizationId, taskId = result.Id }, result);
    }

    /// <summary>
    /// Update an existing task. Only the fields sent change; a body with only <c>status</c> is the
    /// "Change status" action.
    /// </summary>
    // US-TASK-02, US-TASK-03
    [HttpPut("{taskId}")]
    public async Task<IActionResult> Update(
        Guid organizationId,
        Guid taskId,
        [FromBody] UpdateTaskRequest request,
        CancellationToken ct = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId,
            ["TaskId"] = taskId
        });

        logger.LogDebug("Updating task {TaskId}", taskId);

        var result = await taskService.UpdateAsync(AuthenticatedUserId, organizationId, taskId, request, ct);
        return Ok(result);
    }

    /// <summary>Delete a task.</summary>
    // US-TASK-04
    [HttpDelete("{taskId}")]
    public async Task<IActionResult> Delete(
        Guid organizationId,
        Guid taskId,
        CancellationToken ct = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId,
            ["TaskId"] = taskId
        });

        logger.LogDebug("Deleting task {TaskId}", taskId);

        await taskService.DeleteAsync(AuthenticatedUserId, organizationId, taskId, ct);
        return NoContent();
    }
}
