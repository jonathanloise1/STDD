using MyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MyApp.WebApi.Middlewares;

/// <summary>
/// Where impersonation may be on. It signs a request in as any user named by a header, so it can never be on
/// outside the Development environment, whatever the configuration says (the same rule as <c>FaultSwitch</c>).
/// Everything that trusts the impersonation header asks this class.
/// </summary>
public static class ImpersonationSettings
{
    public const string EnabledKey = "MyApp:Impersonation:Enabled";
    public const string DefaultHeaderName = "X-MyApp-Impersonate-UserId";

    /// <summary>True only in the Development environment with the setting turned on.</summary>
    public static bool IsEnabled(IConfiguration configuration, IHostEnvironment environment) =>
        environment.IsDevelopment() && configuration.GetValue<bool>(EnabledKey);

    public static string HeaderName(IConfiguration configuration) =>
        configuration.GetValue<string>("MyApp:Impersonation:HeaderName") ?? DefaultHeaderName;

    /// <summary>True when impersonation is on and the request carries the header.</summary>
    public static bool IsRequested(HttpContext context)
    {
        var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
        var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
        return IsEnabled(configuration, environment) && context.Request.Headers.ContainsKey(HeaderName(configuration));
    }
}

public class ImpersonationMiddleware(
    RequestDelegate next,
    IConfiguration config,
    IHostEnvironment environment,
    ILogger<ImpersonationMiddleware> logger)
{
    private readonly string _headerName = ImpersonationSettings.HeaderName(config);
    private readonly bool _enabled = ImpersonationSettings.IsEnabled(config, environment);

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
                logger.LogInformation("Impersonation header present but no user found with Id {UserId}", userAadId);
            }
        }

        await next(ctx);
    }
}
