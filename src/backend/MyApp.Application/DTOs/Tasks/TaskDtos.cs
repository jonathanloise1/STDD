using MyApp.Domain.Constants;

namespace MyApp.Application.DTOs.Tasks;

/// <summary>
/// DTO returned when listing or fetching a single task.
/// </summary>
// US-TASK-05
public class TaskItemDto
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string Status { get; set; } = default!;
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Paginated result for task listing.
/// </summary>
// US-TASK-05
public class TaskItemPagedResult
{
    public List<TaskItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

/// <summary>
/// Request to create a new task.
/// </summary>
// US-TASK-01
public class CreateTaskRequest
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateOnly? DueDate { get; set; }
}

/// <summary>
/// Request to update an existing task. Only what is sent changes: a field that is absent (null) keeps the
/// stored value, so changing the status never clears the description, the due date or the assignee.
/// A body with only <see cref="Status"/> is the "Change status" action.
/// </summary>
// US-TASK-02, US-TASK-03
public class UpdateTaskRequest
{
    /// <summary>The new title; null keeps the title. A title sent blank is refused.</summary>
    public string? Title { get; set; }

    /// <summary>The new description; null keeps it, an empty text clears it.</summary>
    public string? Description { get; set; }

    public TaskItemStatus? Status { get; set; }

    /// <summary>The new assignee; null keeps it.</summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>The new due date; null keeps it (see <see cref="ClearDueDate"/> to remove it).</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>True removes the due date.</summary>
    public bool ClearDueDate { get; set; }
}
