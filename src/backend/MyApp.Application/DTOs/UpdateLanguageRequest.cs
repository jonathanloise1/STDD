using System.ComponentModel.DataAnnotations;

namespace MyApp.Application.DTOs;

public record UpdateLanguageRequest
{
    [Required, MaxLength(10)]
    public string Language { get; init; } = default!;
}
