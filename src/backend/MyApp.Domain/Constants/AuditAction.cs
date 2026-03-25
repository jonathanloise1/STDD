namespace MyApp.Domain.Constants;

/// <summary>
/// Type of change captured in the audit log.
/// <userstory ref="US-AUD-01, US-AUD-02" />
/// </summary>
public enum AuditAction
{
    /// <summary>Entity was created.</summary>
    Create = 0,

    /// <summary>Entity was updated.</summary>
    Update = 1,

    /// <summary>Entity was deleted.</summary>
    Delete = 2
}
