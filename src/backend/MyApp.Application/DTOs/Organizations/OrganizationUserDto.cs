namespace MyApp.Application.DTOs.Organizations;

/// <summary>
/// Represents a user within an organization, including their identity,
/// role, permissions, and current status.
/// </summary>
/// <userstory ref="US-TEAM-07" />
public class OrganizationUserDto
{
    /// <summary>
    /// Unique identifier of the organization user record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier of the user (User.Id).
    /// Used for update/delete operations.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// First name of the user.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Last name of the user.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Email of the user.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Role: "Admin", "Editor", or "Viewer".
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Current status: "Pending", "Active", or "Disabled".
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
