using System.Data.Common;
using Microsoft.Extensions.Configuration;

namespace MyApp.E2E.Infrastructure;

/// <summary>
/// Centralized configuration for E2E tests.
/// Reads from appsettings.e2e.json and environment variables.
/// </summary>
public static class TestConfiguration
{
    private static readonly IConfiguration _configuration;

    static TestConfiguration()
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.e2e.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    /// <summary>
    /// Frontend base URL (e.g., http://localhost:3147)
    /// </summary>
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASE_URL")
        ?? _configuration["E2E:BaseUrl"]
        ?? "http://localhost:3147";

    /// <summary>
    /// Backend base URL (e.g., https://localhost:7342). Used to wait for the backend and to scope the
    /// impersonation route; test classes never call it.
    /// </summary>
    public static string ApiBaseUrl =>
        Environment.GetEnvironmentVariable("E2E_API_URL")
        ?? _configuration["E2E:ApiBaseUrl"]
        ?? "https://localhost:7342";

    /// <summary>
    /// Default timeout in milliseconds
    /// </summary>
    public static int Timeout =>
        int.TryParse(_configuration["E2E:Timeout"], out var timeout)
            ? timeout
            : 30000;

    /// <summary>
    /// Impersonation header name for backend authentication bypass.
    /// Must match MyApp:Impersonation:HeaderName of the backend (development.md 16.1).
    /// </summary>
    public static string ImpersonationHeaderName =>
        _configuration["E2E:ImpersonationHeaderName"]
        ?? "X-MyApp-Impersonate-UserId";

    /// <summary>
    /// Fault switch header name (development.md 16.2). Only the fault switch helper reads it.
    /// </summary>
    public static string FaultHeaderName =>
        _configuration["E2E:FaultHeaderName"]
        ?? "X-MyApp-Fault";

    /// <summary>
    /// ISO 8601 instant the backend clock starts at (MyApp:Testing:FixedUtcNow), or null for the real clock.
    /// Empty by default: the tests create data and read "newest first" lists, which a clock that restarts at
    /// the same instant on every run would put out of order. Capture classes for the manual set it.
    /// </summary>
    public static string? FixedUtcNow
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("E2E_FIXED_UTC_NOW") ?? _configuration["E2E:FixedUtcNow"];
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    /// <summary>The setting that holds the SQL Server connection string; named in the failure when it is absent.</summary>
    public const string ConnectionStringSetting = "E2E:ConnectionString (environment variable E2E_CONNECTION_STRING)";

    /// <summary>The databases a run may use must end in one of these; the backend checks the same rule.</summary>
    private static readonly string[] DatabaseSuffixes = { "_E2E", "_Test" };

    /// <summary>
    /// The connection string of the SQL Server the E2E run uses. It comes from configuration or the environment
    /// and is never kept in a tracked file. Its database part is ignored: the run always uses
    /// <see cref="DatabaseName"/>. Null when absent.
    /// </summary>
    public static string? ConnectionString
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("E2E_CONNECTION_STRING") ?? _configuration["E2E:ConnectionString"];
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    /// <summary>The dedicated database of the E2E run. Default <c>MyApp_E2E</c>; it must end in _E2E or _Test.</summary>
    public static string DatabaseName
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("E2E_DATABASE_NAME") ?? _configuration["E2E:DatabaseName"];
            return string.IsNullOrWhiteSpace(value) ? "MyApp_E2E" : value.Trim();
        }
    }

    /// <summary>
    /// The connection string the backend is started with: the server of <see cref="ConnectionString"/> and the
    /// database <see cref="DatabaseName"/>. Fails with the name of the missing setting, or when the database name
    /// could belong to something other than a test database. Needs no database access.
    /// </summary>
    public static string BackendConnectionString()
    {
        var server = ConnectionString
            ?? throw new InvalidOperationException(
                $"The E2E run needs a SQL Server connection string, and the setting {ConnectionStringSetting} is not set. " +
                "Its database part is ignored; the run uses its own database (E2E:DatabaseName).");

        var name = DatabaseName;
        if (!DatabaseSuffixes.Any(suffix => name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"E2E:DatabaseName is '{name}'. The E2E run drops its database at start, so the name must end in " +
                $"{string.Join(" or ", DatabaseSuffixes)}.");

        var builder = new DbConnectionStringBuilder { ConnectionString = server };
        builder.Remove("Initial Catalog");
        builder["Database"] = name;
        return builder.ConnectionString;
    }

    /// <summary>
    /// Whether to automatically start frontend and backend before tests.
    /// Default: false (local dev). Override with E2E_AUTO_START_APPS=true for CI.
    /// </summary>
    public static bool AutoStartApps =>
        bool.TryParse(Environment.GetEnvironmentVariable("E2E_AUTO_START_APPS"), out var envAuto)
            ? envAuto
            : bool.TryParse(_configuration["E2E:AutoStartApps"], out var auto)
                ? auto
                : false;

    /// <summary>
    /// Path to the backend project (MyApp.WebApi folder).
    /// Auto-detected from repository structure.
    /// </summary>
    public static string BackendPath => Path.Combine(RepoRoot, "src", "backend", "MyApp.WebApi");

    /// <summary>
    /// Path to the frontend project (src/frontend folder).
    /// Auto-detected from repository structure.
    /// </summary>
    public static string FrontendPath => Path.Combine(RepoRoot, "src", "frontend");

    /// <summary>
    /// Root of the repository, found by walking up from the working directory.
    /// </summary>
    public static string RepoRoot => FindRepoRoot(Directory.GetCurrentDirectory());

    /// <summary>
    /// Whether to stop applications after tests complete.
    /// Default: false (keep running for local dev). Override with E2E_STOP_APPS_AFTER_TESTS=true for CI.
    /// </summary>
    public static bool StopAppsAfterTests =>
        bool.TryParse(Environment.GetEnvironmentVariable("E2E_STOP_APPS_AFTER_TESTS"), out var envStop)
            ? envStop
            : bool.TryParse(_configuration["E2E:StopAppsAfterTests"], out var stop)
                ? stop
                : false;

    /// <summary>
    /// Timeout in seconds for waiting for applications to start.
    /// Default: 120 seconds.
    /// </summary>
    public static int StartupTimeoutSeconds =>
        int.TryParse(_configuration["E2E:StartupTimeoutSeconds"], out var timeout)
            ? timeout
            : 120;

    /// <summary>
    /// Whether tests are running in debug/visual mode (browser visible).
    /// Detected from HEADED=1 environment variable (Playwright standard).
    /// When true, actions like typing are slowed down for visibility.
    /// </summary>
    public static bool IsDebugMode =>
        Environment.GetEnvironmentVariable("HEADED") == "1" ||
        Environment.GetEnvironmentVariable("PWDEBUG") == "1";

    /// <summary>
    /// Delay in milliseconds between keystrokes when typing.
    /// Debug mode (browser visible): 50ms for visibility.
    /// CI/CD mode (headless): 0ms for speed.
    /// </summary>
    public static int TypingDelay => IsDebugMode ? 50 : 0;

    private static string FindRepoRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            // .git folder is the most reliable indicator of repo root
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }
            // Also check for src/backend as a secondary indicator
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "backend")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return startPath;
    }
}
