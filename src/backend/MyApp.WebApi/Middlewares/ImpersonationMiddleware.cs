using MyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MyApp.WebApi.Middlewares;

public class ImpersonationMiddleware(
    RequestDelegate next,
    IConfiguration config,
    ILogger<ImpersonationMiddleware> logger)
{
    private readonly string _headerName = config.GetValue<string>("MyApp:Impersonation:HeaderName") ?? "X-MyApp-Impersonate-UserId";
    private readonly bool _enabled = config.GetValue<bool>("MyApp:Impersonation:Enabled");

    public async Task InvokeAsync(HttpContext ctx, MyAppDbContext db)
    {
        if (_enabled && ctx.Request.Headers.TryGetValue(_headerName, out var userIdString)
            && Guid.TryParse(userIdString, out var userAadId))
        {
            logger.LogDebug("Impersonating user {UserAadId}", userAadId);

            var user = await db.Users.FirstOrDefaultAsync(x => x.AadId == userAadId);

            if (user is not null)
            {
                var claims = new List<Claim>
                {
                    new("http://schemas.microsoft.com/identity/claims/objectidentifier", userAadId.ToString()),
                    new(ClaimTypes.Email, user.Email),
                    new(ClaimTypes.GivenName, user.FirstName),
                    new(ClaimTypes.Surname, user.LastName)
                };

                var identity = new ClaimsIdentity(claims, "Impersonation");
                ctx.User = new ClaimsPrincipal(identity);
            }
            else
            {
                logger.LogWarning("Impersonation header present but no user found with Id {UserId}", userAadId);
            }
        }

        await next(ctx);
    }
}
