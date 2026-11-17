using MyApp.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Seed;

/// <summary>
/// Orchestrates the seeding of test and demo data. Runs after the migrations, in the Development
/// environment only. The dates of the data are relative to <paramref name="clock"/>, so
/// <c>MyApp:Testing:FixedUtcNow</c> makes them the same on every run.
/// </summary>
public static class SeedTestDataOrchestrator
{
    public static async Task InitializeAsync(
        IHostEnvironment env,
        MyAppDbContext context,
        TimeProvider clock,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (!env.IsDevelopment()) return;

        var now = clock.GetUtcNow().UtcDateTime;

        // Users, organizations and memberships (idempotent)
        await SeedLocalDevelopment.SeedAsync(context, logger, now, ct);

        // Tasks and audit history (idempotent)
        await SeedDemoData.SeedAsync(context, logger, now, ct);
    }
}
