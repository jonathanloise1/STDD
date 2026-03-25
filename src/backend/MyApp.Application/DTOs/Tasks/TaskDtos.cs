using MyApp.Domain.Constants;

namespace MyApp.Application.DTOs.Tasks;

/// <summary>
/// DTO returned when listing or fetching a single task.
/// </summary>
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
public class CreateTaskRequest
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateOnly? DueDate { get; set; }
}

/// <summary>
/// Request to update an existing task.
/// </summary>
public class UpdateTaskRequest
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public TaskItemStatus? Status { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateOnly? DueDate { get; set; }
}
