using MyApp.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Seed;

/// <summary>
/// Orchestrates the seeding of test/demo data for development environment.
/// Called after migrations. For local dev user seed, see <see cref="SeedLocalDevelopment"/>.
/// </summary>
public static class SeedTestDataOrchestrator
{
    public static async Task InitializeAsync(
        IHostEnvironment env,
        MyAppDbContext context,
        IConfiguration configuration,
        ILogger? logger = null)
    {
        if (!env.IsDevelopment()) return;

        // Local dev user + organization (idempotent)
        await SeedLocalDevelopment.SeedAsync(context, logger);

        // TODO: Add additional seed data for new domain entities when needed
    }
}

