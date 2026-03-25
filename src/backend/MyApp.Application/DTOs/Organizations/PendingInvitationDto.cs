namespace MyApp.Application.DTOs.Organizations;


/// <summary>
/// Represents a pending invitation for a collaborator in the organization.
/// </summary>
public class PendingInvitationDto
{
    /// <summary>
    /// Unique identifier of the invitation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Full name of the invited collaborator.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Email address of the invited collaborator.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// UTC date when the invitation was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Role assigned to the invited user: "Admin", "Editor", or "Viewer".
    /// </summary>
    public string Role { get; set; } = string.Empty;
}
