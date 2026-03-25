namespace MyApp.Application.DTOs.Organizations;

/// <summary>
/// Request to update the role and permissions of a user in an organization.
/// </summary>
/// <userstory ref="US-TEAM-02, US-TEAM-05" />
public class UpdateOrganizationUserRequest
{
    /// <summary>
    /// Full name of the user (optional - only updates if provided).
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    /// The new role: "Admin", "Editor", or "Viewer".
    /// </summary>
    public string Role { get; set; } = string.Empty;
}
