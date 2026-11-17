using System.Diagnostics;

namespace MyApp.E2E.Infrastructure;

/// <summary>
/// Manages starting and stopping the frontend and backend applications.
/// Used for automatic test environment setup.
/// </summary>
public class ApplicationManager : IAsyncDisposable
{
    private Process? _backendProcess;
    private Process? _frontendProcess;
    private readonly HttpClient _httpClient;
    
    private static ApplicationManager? _instance;
    private static readonly object _lock = new();

    public static ApplicationManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ApplicationManager();
                }
            }
            return _instance;
        }
    }

    private ApplicationManager()
    {
        // Accept any SSL certificate for localhost (dev cert handling)
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>
    /// Start both frontend and backend applications.
    /// </summary>
    public async Task StartApplicationsAsync()
    {
        Console.WriteLine("[E2E] Starting applications...");

        // A run starts from a fresh database, which only a backend started here can reset. One that is already
        // running is on a database nobody reset, so the run stops instead of using it.
        if (await IsBackendRunningAsync())
        {
            throw new InvalidOperationException(
                $"A backend already answers at {TestConfiguration.ApiBaseUrl}. With AutoStartApps on, the run starts " +
                "the backend itself so that it can reset the E2E database. Stop that backend, or set AutoStartApps " +
                "to false and reset the database by hand (see the README).");
        }

        // Start backend first (this also resets the E2E database)
        await StartBackendAsync();

        // Then start frontend
        if (!await IsFrontendRunningAsync())
        {
            await StartFrontendAsync();
        }

        Console.WriteLine("[E2E] Applications started successfully.");
    }

    /// <summary>
    /// Start the .NET backend API.
    /// </summary>
    private async Task StartBackendAsync()
    {
        Console.WriteLine("[E2E] Starting backend...");

        var backendPath = TestConfiguration.BackendPath;
        if (string.IsNullOrEmpty(backendPath) || !Directory.Exists(backendPath))
        {
            throw new InvalidOperationException(
                $"Backend path not configured or doesn't exist: {backendPath}. " +
                "Set E2E:BackendPath in appsettings.e2e.json");
        }

        Console.WriteLine($"[E2E] Backend path: {backendPath}");

        // The backend always starts in Development with the fault switch enabled (development.md 16): the
        // impersonation and the fault switch exist only there. It uses the dedicated E2E database, which it
        // drops, migrates and seeds before it answers; a fixed clock is added when configured.
        var environment = BackendEnvironment();

        // The environment goes to the child through the process itself, never through a shell command line:
        // the connection string holds credentials and must not appear in one.
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --launch-profile https",
            WorkingDirectory = backendPath,
            UseShellExecute = false,
            CreateNoWindow = false
        };
        foreach (var (key, value) in environment)
            startInfo.Environment[key] = value;
        _backendProcess = new Process { StartInfo = startInfo };

        _backendProcess.Start();
        Console.WriteLine($"[E2E] Backend started (PID: {_backendProcess.Id}), database {TestConfiguration.DatabaseName}");

        // The Development backend resets the E2E database, migrates it and loads the seed data (development.md
        // 10.3) before it answers, so a healthy backend means a fresh seed.
        // Wait for backend to be ready via health check
        await WaitForServiceAsync(
            TestConfiguration.ApiBaseUrl + "/health",
            "Backend",
            TimeSpan.FromSeconds(TestConfiguration.StartupTimeoutSeconds),
            _backendProcess);
        
        // Additional stabilization: wait for API to be fully initialized
        // Health endpoint may respond before EF migrations and full initialization
        Console.WriteLine("[E2E] Waiting for backend to fully initialize...");
        await Task.Delay(3000);
        
        // Verify with a real API endpoint
        await WaitForServiceAsync(
            TestConfiguration.ApiBaseUrl + "/swagger/index.html",
            "Backend Swagger",
            TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// The environment variables the backend is started with: Development, the fault switch (development.md
    /// 16.2), the dedicated E2E database with the reset switch (the backend refuses the reset unless the name
    /// ends in _E2E or _Test) and, when <c>E2E:FixedUtcNow</c> is set, the fixed clock.
    /// </summary>
    private static Dictionary<string, string> BackendEnvironment()
    {
        var environment = new Dictionary<string, string>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["MyApp__Testing__FaultInjection__Enabled"] = "true",
            // appsettings.Development.json is git-ignored, so a clean checkout and CI have no impersonation: every
            // signed-in call would get 401.
            ["MyApp__Impersonation__Enabled"] = "true",
            ["ConnectionStrings__DefaultConnection"] = TestConfiguration.BackendConnectionString(),
            ["MyApp__Testing__ResetDatabaseOnStart"] = "true"
        };
        if (TestConfiguration.FixedUtcNow is { } fixedNow)
            environment["MyApp__Testing__FixedUtcNow"] = fixedNow;
        return environment;
    }

    /// <summary>
    /// The settings the frontend is started with. No mock user (an empty value, which beats the one in
    /// <c>.env.development.local</c>, so a visitor really is one) and an identity provider on a host that
    /// belongs to no tenant: <see cref="IdentityProviderStandIn"/> answers it in the visitor's browser, so
    /// "the sign-in page opens" needs nothing from a real tenant.
    /// </summary>
    private static Dictionary<string, string> FrontendEnvironment() => new()
    {
        ["VITE_DEV_MOCK_USER_ID"] = "",
        ["VITE_DEV_MOCK_USER_EMAIL"] = "",
        ["VITE_AZURE_AD_B2C_CLIENT_ID"] = IdentityProviderStandIn.ClientId,
        ["VITE_AZURE_AD_B2C_AUTHORITY"] = IdentityProviderStandIn.Authority,
        ["VITE_AZURE_AD_B2C_KNOWN_AUTHORITIES"] = IdentityProviderStandIn.Host,
        ["VITE_AZURE_AD_B2C_REDIRECT_URI"] = TestConfiguration.BaseUrl,
        ["VITE_API_BASE_URL"] = TestConfiguration.ApiBaseUrl
    };

    /// <summary>
    /// Start the React frontend.
    /// </summary>
    private async Task StartFrontendAsync()
    {
        Console.WriteLine("[E2E] Starting frontend...");

        var frontendPath = TestConfiguration.FrontendPath;
        if (string.IsNullOrEmpty(frontendPath) || !Directory.Exists(frontendPath))
        {
            throw new InvalidOperationException(
                $"Frontend path not configured or doesn't exist: {frontendPath}. " +
                "Set E2E:FrontendPath in appsettings.e2e.json");
        }

        Console.WriteLine($"[E2E] Frontend path: {frontendPath}");

        // Run npm ci first (in hidden window, we wait for it)
        await RunNpmCiAsync(frontendPath);

        var port = new Uri(TestConfiguration.BaseUrl).Port;

        // Every setting that decides how the frontend signs a user in is given here, so the run does not depend
        // on the owner's .env files (the process environment wins over them in Vite).
        var startInfo = new ProcessStartInfo
        {
            FileName = GetNpmCommand(),
            Arguments = "start",
            WorkingDirectory = frontendPath,
            UseShellExecute = false,
            CreateNoWindow = false
        };
        startInfo.Environment["BROWSER"] = "none"; // Playwright controls the browser
        startInfo.Environment["PORT"] = port.ToString();
        foreach (var (key, value) in FrontendEnvironment())
            startInfo.Environment[key] = value;
        _frontendProcess = new Process { StartInfo = startInfo };

        _frontendProcess.Start();
        Console.WriteLine($"[E2E] Frontend started (PID: {_frontendProcess.Id})");

        // Wait for frontend to be ready
        await WaitForServiceAsync(
            TestConfiguration.BaseUrl,
            "Frontend",
            TimeSpan.FromSeconds(TestConfiguration.StartupTimeoutSeconds),
            _frontendProcess);
    }

    /// <summary>
    /// Wait for a service to respond.
    /// </summary>
    private async Task WaitForServiceAsync(string url, string serviceName, TimeSpan timeout, Process? process = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var lastError = "";
        var attemptCount = 0;

        Console.WriteLine($"[E2E] Waiting for {serviceName} at {url}...");

        while (stopwatch.Elapsed < timeout)
        {
            // A service that crashed at start will never answer: say so at once instead of waiting for the timeout.
            if (process is { HasExited: true })
                throw new InvalidOperationException(
                    $"{serviceName} exited with code {process.ExitCode} before answering at {url}. Start it by hand with the same settings to read its output.");

            attemptCount++;
            try
            {
                var response = await _httpClient.GetAsync(url);
                Console.WriteLine($"[E2E] {serviceName} attempt {attemptCount}: {response.StatusCode}");
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"[E2E] {serviceName} is ready at {url} after {stopwatch.Elapsed.TotalSeconds:F1}s");
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                if (attemptCount % 10 == 0) // Log every 10 attempts
                {
                    Console.WriteLine($"[E2E] {serviceName} attempt {attemptCount}: {lastError}");
                }
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException(
            $"{serviceName} did not start within {timeout.TotalSeconds}s. Last error: {lastError}");
    }

    /// <summary>
    /// Check if backend is running.
    /// </summary>
    private async Task<bool> IsBackendRunningAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(TestConfiguration.ApiBaseUrl + "/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if frontend is running.
    /// </summary>
    private async Task<bool> IsFrontendRunningAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(TestConfiguration.BaseUrl);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the npm command based on OS.
    /// </summary>
    private static string GetNpmCommand()
    {
        return OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
    }

    /// <summary>
    /// Run npm ci to install dependencies (skips if node_modules exists)
    /// </summary>
    private async Task RunNpmCiAsync(string frontendPath)
    {
        var nodeModulesPath = Path.Combine(frontendPath, "node_modules");
        
        // Skip if node_modules already exists
        if (Directory.Exists(nodeModulesPath))
        {
            Console.WriteLine("[E2E] node_modules already exists, skipping npm ci");
            return;
        }

        Console.WriteLine("[E2E] Running npm ci to install dependencies...");
        Console.WriteLine($"[E2E] Frontend path: {frontendPath}");

        // Run npm ci in a visible window 
        if (OperatingSystem.IsWindows())
        {
            var npmCiProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"cd /d \"{frontendPath}\" && npm ci\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                }
            };

            npmCiProcess.Start();
            Console.WriteLine($"[E2E] npm ci started in visible window (PID: {npmCiProcess.Id})");
            await npmCiProcess.WaitForExitAsync();

            if (npmCiProcess.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"npm ci failed with exit code {npmCiProcess.ExitCode}. " +
                    "Check if package-lock.json exists and is valid.");
            }
        }
        else
        {
            var npmCiProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetNpmCommand(),
                    Arguments = "ci",
                    WorkingDirectory = frontendPath,
                    UseShellExecute = true,
                    CreateNoWindow = false
                }
            };

            npmCiProcess.Start();
            await npmCiProcess.WaitForExitAsync();

            if (npmCiProcess.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"npm ci failed with exit code {npmCiProcess.ExitCode}. " +
                    "Check if package-lock.json exists and is valid.");
            }
        }

        Console.WriteLine("[E2E] npm ci completed successfully.");
    }

    /// <summary>
    /// Stop all applications.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("[E2E] Stopping applications...");

        await StopProcessAsync(_frontendProcess, "Frontend");
        await StopProcessAsync(_backendProcess, "Backend");

        _httpClient.Dispose();
        _instance = null;
    }

    private static async Task StopProcessAsync(Process? process, string name)
    {
        if (process == null || process.HasExited)
            return;

        try
        {
            Console.WriteLine($"[E2E] Stopping {name}...");
            
            // Try graceful shutdown first
            if (OperatingSystem.IsWindows())
            {
                // Kill process tree on Windows
                using var killer = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/T /F /PID {process.Id}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                killer.Start();
                await killer.WaitForExitAsync();
            }
            else
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();
            Console.WriteLine($"[E2E] {name} stopped.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[E2E] Error stopping {name}: {ex.Message}");
        }
    }
}
