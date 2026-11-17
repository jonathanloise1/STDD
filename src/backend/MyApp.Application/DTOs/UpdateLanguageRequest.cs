using System.ComponentModel.DataAnnotations;

namespace MyApp.Application.DTOs;

public record UpdateLanguageRequest
{
    [Required, RegularExpression("^(en|it)$", ErrorMessage = "Language must be one of: en, it.")]
    public string Language { get; init; } = default!;
}
