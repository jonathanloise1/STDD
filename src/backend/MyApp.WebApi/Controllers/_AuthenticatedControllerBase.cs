using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace MyApp.WebApi.Controllers;

/// <summary>
/// Base controller for authenticated API endpoints.
/// Provides helper properties to extract identity claims from the JWT token.
/// </summary>
[ApiController]
[Authorize(Policy = "RequireAuthenticatedUser")]
[EnableRateLimiting("DefaultPerUser")]
public abstract class AuthenticatedControllerBase : ControllerBase
{
    protected Guid AuthenticatedUserId =>
        Guid.TryParse(User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier"), out var id) ? id : throw new UnauthorizedAccessException("Invalid user ID.");

    protected string AuthenticatedUserEmail =>
        User.FindFirstValue(ClaimTypes.Email) ?? throw new UnauthorizedAccessException("Email not found in token.");

    protected string AuthenticatedUserGivenName =>
        User.FindFirstValue(ClaimTypes.GivenName) ?? throw new UnauthorizedAccessException("Invalid first name.");

    protected string AuthenticatedUserSurname =>
        User.FindFirstValue(ClaimTypes.Surname) ?? throw new UnauthorizedAccessException("Invalid last name.");

    protected string AuthenticatedUserFullname => $"{AuthenticatedUserGivenName} {AuthenticatedUserSurname}";
}
