using MyApp.Application.Interfaces;
using MyApp.Application.Logging;
using MyApp.WebApi.Filters;
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
/// <remarks>
/// Claims extracted from the JWT token:
///   - objectidentifier (sub): unique AAD user GUID
///   - Email: user's email address
///   - GivenName / Surname: user's name (title-cased)
/// </remarks>
// US-AUTH-01, US-AUTH-04, US-AUTH-05
[ApiController]
[Authorize]
[EnableRateLimiting("DefaultPerUser")]
[Route("api/[controller]")]
public class AuthController(
    ILogger<AuthController> logger,
    IAuthenticationService authService) : ControllerBase
{
    /// <summary>
    /// POST /api/auth/sync - Synchronizes the authenticated user with the local database.
    /// Called by the frontend after every successful MSAL login/token acquisition.
    /// </summary>
    /// <remarks>
    /// Validates the JWT and extracts the identity claims, creates the user on first sign-in, auto-accepts
    /// pending organization invitations and returns the organization memberships.
    /// Returns 401 if required claims (objectidentifier, email) are missing.
    /// </remarks>
    // US-AUTH-01, US-AUTH-04, US-AUTH-05
    [HttpPost("sync")]
    [Authorize]
    [ServiceUnavailable(AuthenticationLog.Keys.AuthLoginUnavailable)]
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

        // Missing claims: the 401 is answered as a problem document by the status code pages
        if (userId is null || email is null)
            return Unauthorized();

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = userId
        });

        logger.LogDebug("Sync user request received");

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
