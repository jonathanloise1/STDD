using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using MyApp.Monitor.Pack;

namespace MyApp.Monitor.Dashboard;

/// <summary>Role an app token needs to post to the ingest endpoint (operations.md 10.6).</summary>
public static class DashboardRoles
{
    public const string Ingest = "Incidents.Ingest";
    public const string IngestScheme = "IngestBearer";
    public const string DashboardPolicy = "Dashboard";
    public const string IngestPolicy = "Ingest";
    public const string ConfigSection = "Monitor:Dashboard:AzureAd";
}

/// <summary>Access for the members of one Entra group. The token must carry the <c>groups</c> claim.</summary>
public sealed class GroupRequirement(string groupId) : IAuthorizationRequirement
{
    public string GroupId { get; } = groupId;
}

public sealed class GroupRequirementHandler : AuthorizationHandler<GroupRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, GroupRequirement requirement)
    {
        if (context.User.FindAll("groups").Any(c => string.Equals(c.Value, requirement.GroupId, StringComparison.OrdinalIgnoreCase)))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Dashboard sign-in (operations.md 10.6). <c>local</c> binds to the loopback interface only and asks for no sign-in: it is for a developer
/// machine. <c>entra</c> signs in with Microsoft Entra, lets in the members of <c>dashboard.allowed_group_id</c>, and accepts an app token
/// with the role <c>Incidents.Ingest</c> on the ingest endpoint.
/// </summary>
public static class DashboardAuthentication
{
    public static bool IsLoopbackHost(string host)
    {
        var h = host.Trim('[', ']');
        return h.Equals("localhost", StringComparison.OrdinalIgnoreCase) || (IPAddress.TryParse(h, out var ip) && IPAddress.IsLoopback(ip));
    }

    public static IEnumerable<Uri> ParseUrls(string? urls) =>
        (urls ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(u => Uri.TryCreate(u.Replace("://+", "://wildcard").Replace("://*", "://wildcard"), UriKind.Absolute, out var uri) ? uri : null)
            .OfType<Uri>();

    /// <summary>A reason the dashboard cannot start with these settings, or null. A refusal stops the start with one Critical entry.</summary>
    public static string? Validate(DashboardSettings settings, string? urls, IConfiguration configuration)
    {
        if (!settings.Enabled) return null;
        switch (settings.Auth)
        {
            case "local":
                foreach (var uri in ParseUrls(urls))
                    if (!IsLoopbackHost(uri.Host))
                        return $"dashboard.auth is 'local' (no sign-in), so the dashboard binds to loopback only; '{uri}' is not a loopback address. Use dashboard.auth 'entra' to serve other machines.";
                return null;
            case "entra":
                var section = configuration.GetSection(DashboardRoles.ConfigSection);
                if (string.IsNullOrWhiteSpace(section["ClientId"]) || string.IsNullOrWhiteSpace(section["TenantId"]))
                    return $"dashboard.auth is 'entra' but {DashboardRoles.ConfigSection}:TenantId or :ClientId is missing.";
                if (string.IsNullOrWhiteSpace(settings.AllowedGroupId))
                    return "dashboard.auth is 'entra' but dashboard.allowed_group_id is empty: nobody could sign in.";
                return null;
            default:
                return $"dashboard.auth must be 'local' or 'entra', not '{settings.Auth}'.";
        }
    }

    public static void AddAuthentication(IServiceCollection services, DashboardSettings settings, IConfiguration configuration)
    {
        if (settings.Auth != "entra") return;
        services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(configuration.GetSection(DashboardRoles.ConfigSection))
            .Services
            .AddAuthentication()
            .AddMicrosoftIdentityWebApi(configuration, DashboardRoles.ConfigSection, DashboardRoles.IngestScheme);
        services.AddSingleton<IAuthorizationHandler, GroupRequirementHandler>();
        AddPolicies(services, settings);
    }

    public static void AddPolicies(IServiceCollection services, DashboardSettings settings) =>
        services.AddAuthorization(o =>
        {
            var dashboard = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new GroupRequirement(settings.AllowedGroupId ?? "")).Build();
            o.AddPolicy(DashboardRoles.DashboardPolicy, dashboard);
            o.FallbackPolicy = dashboard;
            o.AddPolicy(DashboardRoles.IngestPolicy, p => p.AddAuthenticationSchemes(DashboardRoles.IngestScheme).RequireAuthenticatedUser().RequireRole(DashboardRoles.Ingest));
        });

    /// <summary>Local mode: only a loopback peer asking for a loopback host name is served (a page on another site cannot reach it by DNS rebinding).</summary>
    public static void UseLoopbackOnly(IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var peer = context.Connection.RemoteIpAddress;
            if ((peer is not null && !IPAddress.IsLoopback(peer)) || !IsLoopbackHost(context.Request.Host.Host))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("The dashboard is open to this machine only (dashboard.auth: local).").ConfigureAwait(false);
                return;
            }
            await next().ConfigureAwait(false);
        });

    public static string UserName(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true
            ? user.FindFirstValue("preferred_username") ?? user.Identity.Name ?? user.FindFirstValue(ClaimTypes.Name) ?? "unknown"
            : Environment.UserName;
}
