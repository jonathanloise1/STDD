using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MyApp.Domain.Constants;

namespace MyApp.Domain.Entities;

/// <summary>
/// Represents a task item within an organization.
/// Demonstrates a typical CRUD entity with status workflow and assignment.
/// <userstory ref="US-TASK-01, US-TASK-02, US-TASK-03" />
/// </summary>
public class TaskItem : IAuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>FK to the owning organization (tenant isolation).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Short title describing the task.</summary>
    [Required, MaxLength(200)]
    public string Title { get; set; } = default!;

    /// <summary>Optional detailed description of the task.</summary>
    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>Current status of the task.</summary>
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;

    /// <summary>FK to the user assigned to this task. Null if unassigned.</summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>Optional due date for the task.</summary>
    public DateOnly? DueDate { get; set; }

    // ── Navigation ───────────────────────────────────────────────────
    [ForeignKey(nameof(OrganizationId))]
    public Organization Organization { get; set; } = default!;

    [ForeignKey(nameof(AssignedToUserId))]
    public User? AssignedToUser { get; set; }

    // IAuditableEntity
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? ModifiedBy { get; set; }
}
