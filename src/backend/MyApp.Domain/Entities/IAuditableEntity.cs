namespace MyApp.Domain.Entities;

/// <summary>
/// Tracks full audit trail: creation + last modification timestamp and author.
/// All auditable entities implement this single interface for simplicity.
/// Fields are set automatically by <see cref="MyApp.Infrastructure.Persistence.AuditableEntityInterceptor"/>.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// UTC timestamp of when the entity was created.
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// AadId (Entra objectidentifier) of the user who created the entity.
    /// Null for system/seed operations.
    /// JOIN with Users.AadId to resolve name/email.
    /// </summary>
    Guid? CreatedBy { get; set; }

    /// <summary>
    /// UTC timestamp of the last modification.
    /// </summary>
    DateTime UpdatedAt { get; set; }

    /// <summary>
    /// AadId (Entra objectidentifier) of the user who last modified the entity.
    /// Null for system/seed operations.
    /// </summary>
    Guid? ModifiedBy { get; set; }
}
