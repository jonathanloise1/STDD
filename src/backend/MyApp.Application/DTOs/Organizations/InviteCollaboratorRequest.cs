namespace MyApp.Application.DTOs.Organizations;

/// <summary>
/// Request to invite a new collaborator into an organization.
/// </summary>
/// <userstory ref="US-TEAM-01, US-TEAM-02, US-TEAM-03, US-TEAM-04" />
public class InviteCollaboratorRequest
{
    /// <summary>
    /// Full name of the collaborator being invited.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Email address of the collaborator being invited.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Role to assign: "Admin", "Editor", or "Viewer".
    /// </summary>
    public string Role { get; set; } = "Viewer";
}
