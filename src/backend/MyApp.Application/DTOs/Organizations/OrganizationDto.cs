using System.ComponentModel.DataAnnotations;

namespace MyApp.Application.DTOs.Organizations;

/// <summary>
/// Represents an organization, including identity, billing details,
/// location, logo, and associated users.
/// </summary>
/// <userstory ref="US-ORG-01, US-ORG-03, US-ORG-04" />
public class OrganizationDto
{
    /// <summary>
    /// Unique identifier of the organization.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the organization.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Legal Name of the organization for invoicing.
    /// </summary>
    public string LegalName { get; set; } = default!;

    /// <summary>
    /// VAT number of the organization (Partita IVA).
    /// </summary>
    public string VatNumber { get; set; } = default!;

    /// <summary>
    /// Fiscal code of the organization (Codice Fiscale).
    /// </summary>
    public string FiscalCode { get; set; } = default!;

    /// <summary>
    /// Billing address or used for invoicing.
    /// </summary>
    public string BillingAddress { get; set; } = default!;

    /// <summary>
    /// Billing city or used for invoicing.
    /// </summary>
    public string BillingCity { get; set; } = default!;

    /// <summary>
    /// Billing province or used for invoicing.
    /// </summary>
    public string BillingProvince { get; set; } = default!;

    /// <summary>
    /// Billing zip code or used for invoicing.
    /// </summary>
    public string BillingZipCode { get; set; } = default!;

    /// <summary>
    /// Billing country code or used for invoicing.
    /// </summary>
    public string BillingCountryCode { get; set; } = default!;

    /// <summary>
    /// Billing email address for invoicing communications.
    /// </summary>
    public string BillingEmail { get; set; } = default!;

    /// <summary>
    /// URL of the organization's logo.
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// List of users associated
    /// with the organization.
    /// </summary>
    public List<OrganizationUserDto> Users { get; set; } = new();
}
