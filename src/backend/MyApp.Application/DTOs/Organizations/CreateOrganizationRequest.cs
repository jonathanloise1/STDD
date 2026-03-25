using System.ComponentModel.DataAnnotations;

namespace MyApp.Application.DTOs.Organizations;

/// <summary>
/// Represents the data required to create a new organization,
/// including identity, tax information, and billing details.
/// </summary>
/// <userstory ref="US-ORG-02" />
/// <userstory ref="US-ONBOARD-CO-03" />
/// <task ref="TASK-OBC-03" />
public class CreateOrganizationRequest
{
    /// <summary>
    /// Updated name of the organization.
    /// </summary>
    public string Name { get; set; } = string.Empty;

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
    /// Optional phone number collected during the onboarding wizard.
    /// Stored on the User entity (User.PhoneNumber), not on the Organization.
    /// </summary>
    /// <userstory ref="US-ONBOARD-CO-03" />
    /// <task ref="TASK-OBC-03" />
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Optional industry template to pre-populate NodeTypes on creation.
    /// Valid values: "manufacturing", "hospitality", "services", "custom" (or null).
    /// When null or "custom", no NodeTypes are seeded.
    /// </summary>
    /// <userstory ref="US-CFG-14" />
    public string? IndustryTemplate { get; set; }
}
