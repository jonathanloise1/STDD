using MyApp.Application.DTOs.Tasks;
using MyApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.WebApi.Controllers;

/// <summary>
/// CRUD endpoints for task items within an organization.
/// <userstory ref="US-TASK-01, US-TASK-02, US-TASK-03" />
/// </summary>
[Route("api/organizations/{organizationId}/tasks")]
public class TasksController(
    ILogger<TasksController> logger,
    ITaskItemService taskService) : AuthenticatedControllerBase
{
    /// <summary>List tasks with optional filters and pagination.</summary>
    /// <userstory ref="US-TASK-02" />
    [HttpGet]
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

        logger.LogDebug("Listing tasks (status={Status}, assignedTo={AssignedTo}, search={Search}, page={Page})",
            status, assignedTo, search, page);

        try
        {
            var result = await taskService.GetByOrganizationAsync(
                AuthenticatedUserId, organizationId, status, assignedTo, search, page, pageSize, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("List tasks failed: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Get a single task by ID.</summary>
    /// <userstory ref="US-TASK-02" />
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

        try
        {
            var result = await taskService.GetByIdAsync(AuthenticatedUserId, organizationId, taskId, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Task not found: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Create a new task.</summary>
    /// <userstory ref="US-TASK-01" />
    [HttpPost]
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

        logger.LogDebug("Creating task '{Title}'", request.Title);

        try
        {
            var result = await taskService.CreateAsync(AuthenticatedUserId, organizationId, request, ct);
            return CreatedAtAction(nameof(GetById), new { organizationId, taskId = result.Id }, result);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Create task failed: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Update an existing task.</summary>
    /// <userstory ref="US-TASK-03" />
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

        try
        {
            var result = await taskService.UpdateAsync(AuthenticatedUserId, organizationId, taskId, request, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Update task failed: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Delete a task.</summary>
    /// <userstory ref="US-TASK-03" />
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

        try
        {
            await taskService.DeleteAsync(AuthenticatedUserId, organizationId, taskId, ct);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Delete task failed: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
    }
}
