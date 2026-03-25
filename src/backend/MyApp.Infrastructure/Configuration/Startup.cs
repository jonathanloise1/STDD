using MyApp.Infrastructure.Persistence;
using MyApp.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Configuration;

public static class Startup
{
    public static async Task InitAsync(
        IServiceProvider serviceProvider,
        IHostEnvironment env,
        CancellationToken cancellationToken = default)
    {
        var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger("MyApp.Infrastructure.Startup");

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MyAppDbContext>();

        logger?.LogInformation("Applying database migrations...");
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger?.LogInformation("Database migrations applied successfully.");

        // Seed local development data (dev user + organization)
        if (env.IsDevelopment())
        {
            logger?.LogInformation("Seeding local development data...");
            await SeedLocalDevelopment.SeedAsync(dbContext, logger, cancellationToken);

            logger?.LogInformation("Seeding demo data...");
            await SeedDemoData.SeedAsync(dbContext, logger, cancellationToken);
        }
    }
}
