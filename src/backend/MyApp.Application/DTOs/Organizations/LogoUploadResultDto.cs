namespace MyApp.Application.DTOs.Organizations;

/// <summary>The result of uploading the organization logo.</summary>
// US-ORG-03
public class LogoUploadResultDto
{
    /// <summary>The address of the new logo, usable as an image source.</summary>
    public string LogoUrl { get; set; } = default!;
}
