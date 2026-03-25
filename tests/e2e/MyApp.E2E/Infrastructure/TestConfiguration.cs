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
    /// Backend API base URL (e.g., http://localhost:5248)
    /// </summary>
    public static string ApiBaseUrl => 
        Environment.GetEnvironmentVariable("E2E_API_URL") 
        ?? _configuration["E2E:ApiBaseUrl"] 
        ?? "http://localhost:5248";

    /// <summary>
    /// Default timeout in milliseconds
    /// </summary>
    public static int Timeout => 
        int.TryParse(_configuration["E2E:Timeout"], out var timeout) 
            ? timeout 
            : 30000;

    /// <summary>
    /// Impersonation secret for API setup/teardown
    /// </summary>
    public static string ImpersonationSecret => 
        Environment.GetEnvironmentVariable("E2E_IMPERSONATION_SECRET") 
        ?? "ABC123";

    /// <summary>
    /// Impersonation header name for backend authentication bypass.
    /// Must match MyApp:Impersonation:HeaderName in appsettings.Development.json.
    /// </summary>
    public static string ImpersonationHeaderName =>
        _configuration["E2E:ImpersonationHeaderName"]
        ?? "X-MyApp-Impersonate-UserId-ABC123";

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
    public static string BackendPath => GetDefaultBackendPath();

    /// <summary>
    /// Path to the frontend project (src/frontend folder).
    /// Auto-detected from repository structure.
    /// </summary>
    public static string FrontendPath => GetDefaultFrontendPath();

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

    private static string GetDefaultBackendPath()
    {
        // Navigate from tests/e2e/MyApp.E2E to src/backend/MyApp.WebApi
        var currentDir = Directory.GetCurrentDirectory();
        var repoRoot = FindRepoRoot(currentDir);
        return Path.Combine(repoRoot, "src", "backend", "MyApp.WebApi");
    }

    private static string GetDefaultFrontendPath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var repoRoot = FindRepoRoot(currentDir);
        return Path.Combine(repoRoot, "src", "frontend");
    }

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
