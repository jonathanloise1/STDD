using System.ComponentModel.DataAnnotations;

namespace MyApp.Domain.Entities;

/// <summary>
/// Represents a platform user authenticated via Microsoft Entra External ID.
/// A user can belong to multiple organizations with different roles.
/// </summary>
public class User : IAuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Azure AD (Entra External ID) object identifier.
    /// </summary>
    [Required]
    public Guid AadId { get; set; }

    [Required, MaxLength(256)]
    public string FirstName { get; set; } = default!;

    [Required, MaxLength(256)]
    public string LastName { get; set; } = default!;

    [Required, MaxLength(256)]
    public string Email { get; set; } = default!;

    [MaxLength(32)]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Preferred UI language (ISO 639-1). Default: "de" (German-speaking Switzerland).</summary>
    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "de";

    /// <summary>Whether the user has completed the onboarding wizard.</summary>
    public bool HasCompletedOnboarding { get; set; }

    /// <summary>
    /// Navigation: organizations this user belongs to.
    /// </summary>
    public List<OrganizationUser> UserOrganizations { get; set; } = new();

    // IAuditableEntity
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? ModifiedBy { get; set; }

    public string GetFullName() => $"{FirstName} {LastName}";
}
