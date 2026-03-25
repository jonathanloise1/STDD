using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.WebApi.Controllers;

/// <summary>
/// User profile endpoints — language, onboarding, profile info.
/// </summary>
[Route("api/[controller]")]
public class UsersController(
    ILogger<UsersController> logger,
    IUserProfileService userProfileService) : AuthenticatedControllerBase
{
    /// <summary>GET /api/users/me/profile — Returns the authenticated user's profile.</summary>
    [HttpGet("me/profile")]
    public async Task<ActionResult<UserProfileDto>> GetProfile(CancellationToken ct)
    {
        logger.LogInformation("Fetching profile for user {UserId}", AuthenticatedUserId);
        var profile = await userProfileService.GetProfileAsync(AuthenticatedUserId, ct);
        return Ok(profile);
    }

    // US-AUTH-08: User sets preferred language
    /// <summary>PUT /api/users/me/language — Updates the user's preferred language.</summary>
    [HttpPut("me/language")]
    public async Task<IActionResult> UpdateLanguage([FromBody] UpdateLanguageRequest request, CancellationToken ct)
    {
        logger.LogInformation("Updating language to {Language} for user {UserId}", request.Language, AuthenticatedUserId);
        await userProfileService.UpdateLanguageAsync(AuthenticatedUserId, request.Language, ct);
        return NoContent();
    }

    /// <summary>POST /api/users/me/complete-onboarding — Marks onboarding as completed.</summary>
    [HttpPost("me/complete-onboarding")]
    public async Task<IActionResult> CompleteOnboarding(CancellationToken ct)
    {
        await userProfileService.CompleteOnboardingAsync(AuthenticatedUserId, ct);
        return NoContent();
    }

    /// <summary>DELETE /api/users/me/complete-onboarding — Resets onboarding state (for testing/support).</summary>
    [HttpDelete("me/complete-onboarding")]
    public async Task<IActionResult> ResetOnboarding(CancellationToken ct)
    {
        await userProfileService.ResetOnboardingAsync(AuthenticatedUserId, ct);
        return NoContent();
    }
}
