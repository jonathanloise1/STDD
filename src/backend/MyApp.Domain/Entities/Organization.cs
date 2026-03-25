using System.ComponentModel.DataAnnotations;

namespace MyApp.Domain.Entities;

/// <summary>
/// Represents an organization with identity, tax, and address details.
/// </summary>
public class Organization : IAuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Display name of the organization.
    /// </summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// Legal name of the organization.
    /// </summary>
    [Required, MaxLength(256)]
    public string LegalName { get; set; } = default!;

    /// <summary>
    /// VAT number.
    /// </summary>
    [MaxLength(50)]
    public string VatNumber { get; set; } = default!;

    /// <summary>
    /// Fiscal code.
    /// </summary>
    [MaxLength(100)]
    public string FiscalCode { get; set; } = default!;

    [MaxLength(250)]
    public string BillingAddress { get; set; } = default!;

    [MaxLength(250)]
    public string BillingCity { get; set; } = default!;

    [MaxLength(250)]
    public string BillingProvince { get; set; } = default!;

    [MaxLength(250)]
    public string BillingZipCode { get; set; } = default!;

    [MaxLength(250)]
    public string BillingCountryCode { get; set; } = default!;

    [MaxLength(150)]
    public string BillingEmail { get; set; } = default!;

    /// <summary>
    /// URL of the organization's logo.
    /// </summary>
    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Marks whether the organization has been soft deleted.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    // Navigation properties

    /// <summary>
    /// Users associated with this organization.
    /// </summary>
    public List<OrganizationUser> Users { get; set; } = new();

    // IAuditableEntity
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? ModifiedBy { get; set; }

    public string BillingFullAddress => $"{BillingAddress}, {BillingCity} ({BillingProvince}) {BillingZipCode}";
}
