using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace MyApp.Application.Services;

public class UserProfileService(
    ILogger<UserProfileService> logger,
    IUsersRepository usersRepository) : IUserProfileService
{
    private static readonly HashSet<string> SupportedLanguages = ["de", "fr", "it", "en"];

    public async Task<UserProfileDto> GetProfileAsync(Guid aadId, CancellationToken ct = default)
    {
        var user = await usersRepository.GetByAadIdAsync(aadId, ct)
            ?? throw new KeyNotFoundException($"User not found for AadId {aadId}");

        return new UserProfileDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PreferredLanguage = user.PreferredLanguage,
            HasCompletedOnboarding = user.HasCompletedOnboarding
        };
    }

    public async Task UpdateLanguageAsync(Guid aadId, string language, CancellationToken ct = default)
    {
        if (!SupportedLanguages.Contains(language))
            throw new ArgumentException($"Unsupported language: {language}. Supported: {string.Join(", ", SupportedLanguages)}");

        var user = await usersRepository.GetByAadIdAsync(aadId, ct)
            ?? throw new KeyNotFoundException($"User not found for AadId {aadId}");

        user.PreferredLanguage = language;
        await usersRepository.UpdateAsync(user, ct);

        logger.LogInformation("Language updated to {Language} for user {AadId}", language, aadId);
    }

    public async Task CompleteOnboardingAsync(Guid aadId, CancellationToken ct = default)
    {
        var user = await usersRepository.GetByAadIdAsync(aadId, ct)
            ?? throw new KeyNotFoundException($"User not found for AadId {aadId}");

        user.HasCompletedOnboarding = true;
        await usersRepository.UpdateAsync(user, ct);

        logger.LogInformation("Onboarding completed for user {AadId}", aadId);
    }

    public async Task ResetOnboardingAsync(Guid aadId, CancellationToken ct = default)
    {
        var user = await usersRepository.GetByAadIdAsync(aadId, ct)
            ?? throw new KeyNotFoundException($"User not found for AadId {aadId}");

        user.HasCompletedOnboarding = false;
        user.PreferredLanguage = "de";
        await usersRepository.UpdateAsync(user, ct);

        logger.LogInformation("Onboarding reset for user {AadId}", aadId);
    }
}
