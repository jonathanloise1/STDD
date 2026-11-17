using System.Security.Claims;
using MyApp.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace MyApp.Infrastructure.Services;

/// <summary>
/// Resolves the current user's AadId from <see cref="HttpContext"/> claims.
/// Returns null when there is no authenticated user (system/seed operations).
/// Registered as Scoped so each HTTP request gets its own instance.
/// </summary>
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private const string ObjectIdentifierClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    /// <inheritdoc />
    public Guid? UserId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User.FindFirst(ObjectIdentifierClaim)?.Value;
            return claim is not null && Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
