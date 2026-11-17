using MyApp.Application.Help;
using MyApp.Application.Interfaces;
using MyApp.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Application.Configuration;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application-level services, including business logic and configuration mappings.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IOrganizationsService, OrganizationsService>();

        // Audit
        services.AddScoped<IAuditService, AuditService>();

        // User profile
        services.AddScoped<IUserProfileService, UserProfileService>();

        // Tasks (example domain)
        services.AddScoped<ITaskItemService, TaskItemService>();

        // Dashboard
        services.AddScoped<IDashboardService, DashboardService>();

        // Help: configuration keys and documentation gaps (the pack, the lock and the model host are wired by the web layer)
        services.AddSingleton(HelpSettings.Load(configuration));
        services.AddScoped<HelpGapService>();


        return services;
    }
}
