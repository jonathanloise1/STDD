using MyApp.Application.Interfaces;
using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace MyApp.WebApi.Controllers;

/// <summary>
/// Handles authentication-related API endpoints.
/// Validates JWT tokens issued by Azure AD B2C (Entra External ID) and orchestrates
/// user synchronization with the local database.
/// </summary>
/// <userstory ref="US-AUTH-01, US-AUTH-02, US-AUTH-04" />
/// <remarks>
/// Claims extracted from the JWT token:
///   - objectidentifier (sub): unique AAD user GUID
///   - Email: user's email address
///   - GivenName / Surname: user's name (title-cased)
/// </remarks>
[ApiController]
[Authorize]
[EnableRateLimiting("DefaultPerUser")]
[Route("api/[controller]")]
public class AuthController(
    ILogger<AuthController> logger,
    IAuthenticationService authService) : ControllerBase
{
    /// <summary>
    /// POST /api/auth/sync — Synchronizes the authenticated user with the local database.
    /// Called by the frontend after every successful MSAL login/token acquisition.
    /// </summary>
    /// <userstory ref="US-AUTH-01, US-AUTH-02, US-AUTH-04, US-AUTH-05" />
    /// <remarks>
    /// This single endpoint covers multiple user stories:
    ///   - US-AUTH-01 (Login): validates the JWT and extracts identity claims.
    ///   - US-AUTH-02 (Registration): if the user doesn't exist, delegates creation to AuthenticationService.
    ///   - US-AUTH-04 (Sync): returns subscription features, plan type, and organization memberships.
    ///   - US-AUTH-05 (Invitation): auto-accepts pending org invitations (handled inside AuthenticationService).
    /// Returns 401 if required claims (objectidentifier, email) are missing.
    /// 
    /// Accepts optional organizationId in request body to return features for that specific organization.
    /// If not provided, returns features for the first/primary organization.
    /// </remarks>
    [HttpPost("sync")]
    [Authorize]
    public async Task<IActionResult> SyncUserAsync([FromBody] SyncUserRequest? request, CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value?.ToLowerInvariant();

        var name = User.FindFirst(ClaimTypes.GivenName)?.Value?
            .Trim()
            .Transform(To.TitleCase) ?? string.Empty;

        var lastName = User.FindFirst(ClaimTypes.Surname)?.Value?
            .Trim()
            .Transform(To.TitleCase) ?? string.Empty;

        if (userId is null || email is null)
        {
            logger.LogWarning("Sync failed — missing required claims (objectidentifier or email)");
            return Unauthorized();
        }

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = userId,
            ["Email"] = email
        });

        logger.LogDebug("Sync user request received");

        try
        {
            var result = await authService.SyncUserAsync(
                Guid.Parse(userId),
                name,
                lastName,
                email,
                request?.OrganizationId,
                cancellationToken);

            logger.LogInformation("User synced successfully");

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during user sync");
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }
}

/// <summary>
/// Request body for POST /api/auth/sync endpoint.
/// </summary>
public record SyncUserRequest
{
    /// <summary>
    /// Optional organization ID. If provided, returns features for that specific organization.
    /// If null, returns features for the first/primary organization.
    /// </summary>
    public Guid? OrganizationId { get; init; }
}
