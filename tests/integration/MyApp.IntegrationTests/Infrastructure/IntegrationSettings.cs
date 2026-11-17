using System.Data.Common;
using Microsoft.Extensions.Configuration;
using MyApp.Infrastructure.Configuration;

namespace MyApp.IntegrationTests.Infrastructure;

/// <summary>
/// Settings of the integration tests (development.md 10.1). Integration tests run the backend against a
/// real SQL Server, so the connection string is a setting the person running the tests provides. It is never
/// written in code. Its database part is ignored: the run has its own database, <see cref="DatabaseName"/>
/// (default <c>MyApp_Integration_Test</c>), which the backend drops, migrates and seeds when it starts.
///
/// Sources, later ones win: <c>appsettings.integration.json</c> (tracked, empty),
/// <c>appsettings.integration.local.json</c> (git-ignored), environment variables
/// <c>IntegrationTests__ConnectionString</c> and <c>IntegrationTests__DatabaseName</c>.
/// </summary>
public static class IntegrationSettings
{
    public const string ConnectionStringKey = "IntegrationTests:ConnectionString";
    public const string DatabaseNameKey = "IntegrationTests:DatabaseName";
    public const string DefaultDatabaseName = "MyApp_Integration_Test";

    /// <summary>Header the impersonation middleware reads (development.md 16.1).</summary>
    public const string ImpersonationHeaderName = "X-MyApp-Impersonate-UserId";

    /// <summary>Header of the fault switch (development.md 16.2).</summary>
    public const string FaultHeaderName = "X-MyApp-Fault";

    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.integration.json", optional: true)
        .AddJsonFile("appsettings.integration.local.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    /// <summary>The SQL Server connection string given to the run, or null when absent. Its database part is ignored.</summary>
    public static string? ServerConnectionString
    {
        get
        {
            var value = Configuration[ConnectionStringKey];
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    /// <summary>The dedicated database of the run. Default <c>MyApp_Integration_Test</c>; it must end in _Test or _E2E.</summary>
    public static string DatabaseName
    {
        get
        {
            var value = Configuration[DatabaseNameKey];
            return string.IsNullOrWhiteSpace(value) ? DefaultDatabaseName : value.Trim();
        }
    }

    /// <summary>
    /// The connection string the backend runs with: the server of <see cref="ServerConnectionString"/> and the
    /// database <see cref="DatabaseName"/>. Fails with the name of the missing setting, or when the database
    /// name could belong to something other than a test database. Needs no database access.
    /// </summary>
    public static string ConnectionString
    {
        get
        {
            var server = ServerConnectionString
                ?? throw new InvalidOperationException(
                    $"The integration tests need a SQL Server connection string, and the setting {ConnectionStringKey} " +
                    "(environment variable IntegrationTests__ConnectionString) is not set. You can also set it in " +
                    "tests/integration/MyApp.IntegrationTests/appsettings.integration.local.json (git-ignored). " +
                    $"Its database part is ignored; the run uses its own database ({DatabaseNameKey}, default {DefaultDatabaseName}).");

            var name = DatabaseName;
            var suffixes = TestDatabaseReset.AllowedSuffixes;
            if (!suffixes.Any(suffix => name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    $"{DatabaseNameKey} is '{name}'. The run drops its database at start, so the name must end in " +
                    $"{string.Join(" or ", suffixes)}.");

            var builder = new DbConnectionStringBuilder { ConnectionString = server };
            builder.Remove("Initial Catalog");
            builder["Database"] = name;
            return builder.ConnectionString;
        }
    }
}
