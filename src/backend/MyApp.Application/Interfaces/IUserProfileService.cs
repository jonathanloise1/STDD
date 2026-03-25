using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

public interface IUserProfileService
{
    Task<UserProfileDto> GetProfileAsync(Guid aadId, CancellationToken ct = default);
    Task UpdateLanguageAsync(Guid aadId, string language, CancellationToken ct = default);
    Task CompleteOnboardingAsync(Guid aadId, CancellationToken ct = default);
    Task ResetOnboardingAsync(Guid aadId, CancellationToken ct = default);
}
