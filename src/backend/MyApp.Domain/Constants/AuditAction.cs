namespace MyApp.Domain.Constants;

/// <summary>
/// Type of change captured in the audit log.
/// </summary>
// US-AUD-01
public enum AuditAction
{
    /// <summary>Entity was created.</summary>
    Create = 0,

    /// <summary>Entity was updated.</summary>
    Update = 1,

    /// <summary>Entity was deleted.</summary>
    Delete = 2
}
