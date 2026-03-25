using System.ComponentModel.DataAnnotations;

namespace MyApp.Application.DTOs.Organizations;

/// <summary>
/// Represents the data used to update an existing organization,
/// including identity, tax, billing, and address information.
/// </summary>
/// <userstory ref="US-ORG-04" />
public class UpdateOrganizationRequest
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

}
