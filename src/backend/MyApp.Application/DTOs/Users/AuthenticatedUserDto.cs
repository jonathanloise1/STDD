namespace MyApp.Application.DTOs.Users;

/// <summary>
/// DTO representing an authenticated user after successful login or sync.
/// Returned by POST /api/auth/sync to the frontend.
/// </summary>
public class AuthenticatedUserDto
{
    /// <summary>
    /// Unique identifier of the authenticated user.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Email address of the authenticated user.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    // US-AUTH-08: Language preference returned in sync response
    /// <summary>
    /// User's preferred UI language (ISO 639-1: de, fr, it, en).
    /// </summary>
    public string PreferredLanguage { get; set; } = "de";

    // US-ONBOARD-01: Onboarding state returned in sync response
    /// <summary>
    /// Whether the user has completed the onboarding wizard.
    /// </summary>
    public bool HasCompletedOnboarding { get; set; }

    /// <summary>
    /// List of organizations the user belongs to with their role and permissions.
    /// </summary>
    public List<AuthenticatedUserOrganizationDto> Organizations { get; set; } = [];
}
