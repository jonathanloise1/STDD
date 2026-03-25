using System.ComponentModel.DataAnnotations;
using MyApp.Domain.Constants;

namespace MyApp.Domain.Entities;

/// <summary>
/// Append-only audit record capturing every data change via EF Core SaveChanges interceptor.
/// <userstory ref="US-AUD-01, US-AUD-02, US-AUD-03" />
/// </summary>
public class AuditLog
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>FK to the organization for tenant isolation.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Entity class name, e.g. "Node", "CostEntry".</summary>
    [Required, MaxLength(100)]
    public string EntityType { get; set; } = default!;

    /// <summary>String representation of the entity PK.</summary>
    [Required, MaxLength(100)]
    public string EntityId { get; set; } = default!;

    /// <summary>Create, Update, Delete.</summary>
    public AuditAction Action { get; set; }

    /// <summary>FK to the user who triggered the change.</summary>
    public Guid UserId { get; set; }

    /// <summary>Denormalized display name for queries without join.</summary>
    [MaxLength(200)]
    public string? UserDisplayName { get; set; }

    /// <summary>JSON of original values (null for Create).</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON of current values (null for Delete).</summary>
    public string? NewValues { get; set; }

    /// <summary>JSON list of property names that changed (for Update).</summary>
    public string? ChangedProperties { get; set; }

    /// <summary>UTC timestamp of the change.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Groups related changes from a single operation.</summary>
    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    // ── Navigation ───────────────────────────────────────────────────
    public Organization Organization { get; set; } = default!;
    public User User { get; set; } = default!;
}
