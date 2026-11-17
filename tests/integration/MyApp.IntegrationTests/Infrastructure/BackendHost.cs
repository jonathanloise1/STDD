using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MyApp.WebApi.Filters;
using NUnit.Framework;

namespace MyApp.IntegrationTests;

/// <summary>
/// Starts the backend once for the whole run, in the Development environment and against the database of
/// <see cref="Infrastructure.IntegrationSettings"/>. The backend drops that database, migrates it and loads
/// the seed data when it starts, exactly as it does for the E2E run, so every run starts from the same data. Tests reach it over HTTP through
/// <see cref="Infrastructure.Api"/>.
///
/// The test switches are on, as the E2E run has them: the reset, the impersonation header (development.md 16.1) and
/// the fault switch (16.2). They exist only in the Development environment.
/// </summary>
[SetUpFixture]
public class BackendHost
{
    private static WebApplicationFactory<FaultSwitch>? _factory;

    /// <summary>A client of the running backend. It carries no identity; <c>Api</c> adds one per request.</summary>
    public static HttpClient Client { get; private set; } = null!;

    /// <summary>The services of the running backend, for tests that assert stored state.</summary>
    public static IServiceProvider Services =>
        _factory?.Services ?? throw new InvalidOperationException("The backend is not running.");

    [OneTimeSetUp]
    public void StartBackend()
    {
        var connectionString = Infrastructure.IntegrationSettings.ConnectionString;

        // Environment variables are read when the host builds its configuration, before the application
        // reads the connection string, which a later override of the configuration would miss.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connectionString);
        // The backend drops, migrates and seeds the dedicated database at start (development.md 10.3); the guard
        // of TestDatabaseReset refuses any database not ending in _Test or _E2E.
        Environment.SetEnvironmentVariable("MyApp__Testing__ResetDatabaseOnStart", "true");
        Environment.SetEnvironmentVariable("MyApp__Impersonation__Enabled", "true");
        Environment.SetEnvironmentVariable("MyApp__Impersonation__HeaderName", Infrastructure.IntegrationSettings.ImpersonationHeaderName);
        Environment.SetEnvironmentVariable("MyApp__Testing__FaultInjection__Enabled", "true");

        // Any public type of the backend assembly points the factory at it; the entry point is top-level code
        _factory = new WebApplicationFactory<FaultSwitch>();
        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    [OneTimeTearDown]
    public void StopBackend()
    {
        Client?.Dispose();
        _factory?.Dispose();
        _factory = null;
    }
}
