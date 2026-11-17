using MyApp.Infrastructure.Persistence;
using MyApp.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

        // E2E runs only: drop the database so that migrations and seed start from nothing (development.md 10.3).
        // The guard allows it in Development, with the switch on, for a database ending in _E2E or _Test.
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (TestDatabaseReset.IsRequested(configuration))
        {
            var connectionString = dbContext.Database.GetConnectionString();
            var decision = TestDatabaseReset.Evaluate(configuration, env, connectionString);
            if (!decision.Allowed)
            {
                throw new InvalidOperationException(
                    $"{TestDatabaseReset.SwitchKey} is set but the database is not reset. {decision.Reason}");
            }

            logger?.LogInformation("Dropping the test database '{Database}' before the run.", TestDatabaseReset.DatabaseName(connectionString));
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        }

        logger?.LogInformation("Applying database migrations...");
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger?.LogInformation("Database migrations applied successfully.");

        // Seed data (Development only): users, organizations, tasks and the audit history
        if (env.IsDevelopment())
        {
            logger?.LogInformation("Seeding development data...");
            await SeedTestDataOrchestrator.InitializeAsync(
                env, dbContext, scope.ServiceProvider.GetRequiredService<TimeProvider>(), logger, cancellationToken);
        }
    }
}
