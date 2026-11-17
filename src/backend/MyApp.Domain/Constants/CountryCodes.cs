namespace MyApp.Domain.Constants;

/// <summary>
/// Supported countries with ISO 3166-1 alpha-2 codes and phone codes.
/// </summary>
public static class CountryCodes
{
    public static readonly Dictionary<string, CountryInfo> SupportedCountries = new()
    {
        // EU Countries (27)
        { "AT", new CountryInfo("Austria", "+43", "🇦🇹") },
        { "BE", new CountryInfo("Belgium", "+32", "🇧🇪") },
        { "BG", new CountryInfo("Bulgaria", "+359", "🇧🇬") },
        { "HR", new CountryInfo("Croatia", "+385", "🇭🇷") },
        { "CY", new CountryInfo("Cyprus", "+357", "🇨🇾") },
        { "CZ", new CountryInfo("Czech Republic", "+420", "🇨🇿") },
        { "DK", new CountryInfo("Denmark", "+45", "🇩🇰") },
        { "EE", new CountryInfo("Estonia", "+372", "🇪🇪") },
        { "FI", new CountryInfo("Finland", "+358", "🇫🇮") },
        { "FR", new CountryInfo("France", "+33", "🇫🇷") },
        { "DE", new CountryInfo("Germany", "+49", "🇩🇪") },
        { "GR", new CountryInfo("Greece", "+30", "🇬🇷") },
        { "HU", new CountryInfo("Hungary", "+36", "🇭🇺") },
        { "IE", new CountryInfo("Ireland", "+353", "🇮🇪") },
        { "IT", new CountryInfo("Italy", "+39", "🇮🇹") },
        { "LV", new CountryInfo("Latvia", "+371", "🇱🇻") },
        { "LT", new CountryInfo("Lithuania", "+370", "🇱🇹") },
        { "LU", new CountryInfo("Luxembourg", "+352", "🇱🇺") },
        { "MT", new CountryInfo("Malta", "+356", "🇲🇹") },
        { "NL", new CountryInfo("Netherlands", "+31", "🇳🇱") },
        { "PL", new CountryInfo("Poland", "+48", "🇵🇱") },
        { "PT", new CountryInfo("Portugal", "+351", "🇵🇹") },
        { "RO", new CountryInfo("Romania", "+40", "🇷🇴") },
        { "SK", new CountryInfo("Slovakia", "+421", "🇸🇰") },
        { "SI", new CountryInfo("Slovenia", "+386", "🇸🇮") },
        { "ES", new CountryInfo("Spain", "+34", "🇪🇸") },
        { "SE", new CountryInfo("Sweden", "+46", "🇸🇪") },
    };

    /// <summary>
    /// EU countries with strict phone validation support (27 EU member states only).
    /// </summary>
    public static readonly HashSet<string> EuCountries = new()
    {
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR",
        "DE", "GR", "HU", "IE", "IT", "LV", "LT", "LU", "MT", "NL",
        "PL", "PT", "RO", "SK", "SI", "ES", "SE"
    };

    /// <summary>
    /// Get country phone code by ISO code.
    /// </summary>
    public static string? GetPhoneCode(string isoCode)
    {
        return SupportedCountries.TryGetValue(isoCode, out var info) ? info.PhoneCode : null;
    }

    /// <summary>
    /// Get country name by ISO code.
    /// </summary>
    public static string? GetCountryName(string isoCode)
    {
        return SupportedCountries.TryGetValue(isoCode, out var info) ? info.Name : null;
    }

    /// <summary>
    /// Validate if country code is supported.
    /// </summary>
    public static bool IsSupported(string isoCode)
    {
        return SupportedCountries.ContainsKey(isoCode);
    }

    /// <summary>
    /// Check if country supports phone validation (EU countries only).
    /// </summary>
    public static bool IsEuCountry(string isoCode)
    {
        return EuCountries.Contains(isoCode);
    }

    /// <summary>
    /// Get all supported countries as a list.
    /// </summary>
    public static List<CountryInfo> GetAllCountries()
    {
        return SupportedCountries
            .Select(c => new CountryInfo(c.Value.Name, c.Value.PhoneCode, c.Value.FlagEmoji) { Code = c.Key })
            .OrderBy(c => c.Name)
            .ToList();
    }
}

/// <summary>
/// Information about a country including name, phone code, and flag emoji.
/// </summary>
public record CountryInfo(string Name, string PhoneCode, string FlagEmoji)
{
    /// <summary>
    /// ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string Code { get; init; } = string.Empty;
}
