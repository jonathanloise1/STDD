using MyApp.Application.Interfaces;
using MyApp.Domain.Repositories;
using MyApp.Infrastructure.Jobs;
using MyApp.Infrastructure.Persistence;
using MyApp.Infrastructure.Repositories;
using MyApp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Current user service (reads AadId from HttpContext for audit fields)
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddDbContext<MyAppDbContext>((serviceProvider, options) =>
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var currentUserService = serviceProvider.GetRequiredService<ICurrentUserService>();

            var dbOptions = options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"))
                .UseLoggerFactory(loggerFactory)
                .AddInterceptors(
                    new AuditableEntityInterceptor(currentUserService),
                    new AuditCdcInterceptor(currentUserService));

            // SECURITY: Only enable sensitive data logging in development (GDPR compliance)
            if (environment.IsDevelopment())
            {
                dbOptions.EnableSensitiveDataLogging()
                    .EnableDetailedErrors();
            }
        });

        // Repositories
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IOrganizationsRepository, OrganizationsRepository>();
        services.AddScoped<ITaskItemsRepository, TaskItemsRepository>();

        // Audit
        services.AddScoped<IAuditLogsRepository, AuditLogsRepository>();

        // Background jobs
        services.AddHostedService<AuditRetentionCleanupJob>();
        services.AddHostedService<OverdueTaskNotificationJob>();

        return services;
    }
}
