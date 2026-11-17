using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyApp.Domain.Entities;

/// <summary>
/// Defines the role of a user within an organization.
/// </summary>
public enum OrganizationRole
{
    /// <summary>Full access to all features and organization management.</summary>
    Admin = 0,
    /// <summary>Can create, edit, and delete data.</summary>
    Editor = 1,
    /// <summary>Read-only access.</summary>
    Viewer = 2
}

/// <summary>
/// Indicates the current status of a user in the organization lifecycle.
/// </summary>
public enum OrganizationUserStatus
{
    Pending = 0,      // Invited but not yet registered
    Active = 1,       // Fully onboarded
    Disabled = 2      // Manually removed or disabled
}

/// <summary>
/// Represents the link between a User and an Organization, with role and status.
/// </summary>
public class OrganizationUser : IAuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the User entity. Null when the invitation is still pending.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Foreign key to the owning Organization.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Email of the user. Always set — used for matching pending invitations on registration.
    /// </summary>
    [Required, MaxLength(256)]
    public string Email { get; set; } = default!;

    /// <summary>
    /// Display name for the user within this organization.
    /// Set at invitation time; updated from User entity once active.
    /// </summary>
    [MaxLength(512)]
    public string? FullName { get; set; }

    /// <summary>
    /// Defines the user's access level: Admin (full), Editor (read/write), or Viewer (read-only).
    /// </summary>
    [Required]
    public OrganizationRole Role { get; set; }

    /// <summary>
    /// Current status of the organization user (Pending, Active, Disabled).
    /// </summary>
    public OrganizationUserStatus Status { get; set; } = OrganizationUserStatus.Pending;

    // Navigation properties

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    public Organization? Organization { get; set; }

    // IAuditableEntity
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? ModifiedBy { get; set; }
}
