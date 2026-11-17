namespace MyApp.Application.DTOs.Users;

/// <summary>
/// DTO representing organization membership details for an authenticated user.
/// </summary>
public class AuthenticatedUserOrganizationDto
{
    /// <summary>
    /// The organization ID.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The organization name.
    /// </summary>
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>
    /// The user's role in the organization: "Admin", "Editor", or "Viewer".
    /// </summary>
    public string Role { get; set; } = string.Empty;
}
