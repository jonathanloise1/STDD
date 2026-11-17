using MyApp.Domain.Entities;

namespace MyApp.Application.DTOs.Organizations;

/// <summary>
/// Hand-written mappings between organization entities and DTOs.
/// </summary>
// US-ORG-01, US-ORG-02, US-ORG-04, US-ONBOARD-02
public static class OrganizationMappings
{
    public static OrganizationDto ToDto(this Organization organization) => new()
    {
        Id = organization.Id,
        Name = organization.Name,
        LegalName = organization.LegalName,
        VatNumber = organization.VatNumber,
        FiscalCode = organization.FiscalCode,
        BillingAddress = organization.BillingAddress,
        BillingCity = organization.BillingCity,
        BillingProvince = organization.BillingProvince,
        BillingZipCode = organization.BillingZipCode,
        BillingCountryCode = organization.BillingCountryCode,
        BillingEmail = organization.BillingEmail,
        LogoUrl = organization.LogoUrl
    };

    public static OrganizationUserDto ToDto(this OrganizationUser member) => new()
    {
        Id = member.Id,
        Role = member.Role.ToString(),
        Status = member.Status.ToString(),
        UserId = member.User?.Id ?? Guid.Empty,
        FirstName = member.User is not null ? member.User.FirstName : (member.FullName ?? ""),
        LastName = member.User is not null ? member.User.LastName : "",
        Email = member.User is not null ? member.User.Email : member.Email
    };

    public static Organization ToEntity(this CreateOrganizationRequest request)
    {
        var organization = new Organization();
        organization.Apply(request.Name, request.LegalName, request.VatNumber, request.FiscalCode,
            request.BillingAddress, request.BillingCity, request.BillingProvince, request.BillingZipCode,
            request.BillingCountryCode, request.BillingEmail);
        return organization;
    }

    public static void ApplyTo(this UpdateOrganizationRequest request, Organization organization) =>
        organization.Apply(request.Name, request.LegalName, request.VatNumber, request.FiscalCode,
            request.BillingAddress, request.BillingCity, request.BillingProvince, request.BillingZipCode,
            request.BillingCountryCode, request.BillingEmail);

    private static void Apply(this Organization o, string name, string legalName, string vatNumber,
        string fiscalCode, string address, string city, string province, string zipCode,
        string countryCode, string email)
    {
        o.Name = name;
        o.LegalName = legalName;
        o.VatNumber = vatNumber;
        o.FiscalCode = fiscalCode;
        o.BillingAddress = address;
        o.BillingCity = city;
        o.BillingProvince = province;
        o.BillingZipCode = zipCode;
        o.BillingCountryCode = countryCode;
        o.BillingEmail = email;
    }
}
