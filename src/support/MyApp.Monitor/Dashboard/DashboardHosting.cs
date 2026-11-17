using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Monitor.Pack;

namespace MyApp.Monitor.Dashboard;

/// <summary>ASP.NET Core Razor Pages inside the monitor (operations.md 10.6): server-rendered, no front-end build step, reading the store only.</summary>
public static class DashboardHosting
{
    public static void AddDashboard(IServiceCollection services, DashboardSettings settings, IConfiguration configuration)
    {
        services.AddRazorPages(o => o.RootDirectory = "/Dashboard/Pages");
        DashboardAuthentication.AddAuthentication(services, settings, configuration);
    }

    public static void Map(WebApplication app, DashboardSettings settings)
    {
        var entra = settings.Auth == "entra";
        if (!entra) DashboardAuthentication.UseLoopbackOnly(app);
        app.UseRouting();
        if (entra) app.UseAuthentication();
        app.UseAuthorization();
        app.MapRazorPages();
        DashboardApi.Map(app, entra);
    }
}
