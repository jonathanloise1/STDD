namespace MyApp.WebApi.Authorization;

/// <summary>
/// Represents the required role level for authorization policies.
/// </summary>
public enum OrganizationPermission
{
    /// <summary>
    /// Requires Admin role (full access).
    /// </summary>
    Admin,

    /// <summary>
    /// Requires at least Editor role (read/write).
    /// </summary>
    Editor,

    /// <summary>
    /// Requires at least Viewer role (read-only).
    /// </summary>
    Viewer,
}
