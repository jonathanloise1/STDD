namespace MyApp.Application.DTOs;

public record UserProfileDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = default!;
    public string LastName { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string PreferredLanguage { get; init; } = default!;
    public bool HasCompletedOnboarding { get; init; }
}
