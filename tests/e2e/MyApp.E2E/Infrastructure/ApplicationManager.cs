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

        // Check if apps are already running
        if (await IsBackendRunningAsync() && await IsFrontendRunningAsync())
        {
            Console.WriteLine("[E2E] Applications already running, skipping startup.");
            return;
        }

        // Start backend first
        if (!await IsBackendRunningAsync())
        {
            await StartBackendAsync();
        }

        // Then start frontend
        if (!await IsFrontendRunningAsync())
        {
            await StartFrontendAsync();
        }

        Console.WriteLine("[E2E] Applications started successfully.");
    }

    /// <summary>
    /// Start the .NET backend API in a visible terminal window.
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

        // Start backend in a visible terminal window
        if (OperatingSystem.IsWindows())
        {
            _backendProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k \"cd /d \"{backendPath}\" && set ASPNETCORE_ENVIRONMENT=Development && dotnet run --launch-profile https\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                }
            };
        }
        else
        {
            // For Linux/Mac, open in a new terminal
            _backendProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run --launch-profile https",
                    WorkingDirectory = backendPath,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    Environment = { ["ASPNETCORE_ENVIRONMENT"] = "Development" }
                }
            };
        }

        _backendProcess.Start();
        Console.WriteLine($"[E2E] Backend terminal opened (PID: {_backendProcess.Id})");

        // Wait for backend to be ready via health check
        await WaitForServiceAsync(
            TestConfiguration.ApiBaseUrl + "/health",
            "Backend",
            TimeSpan.FromSeconds(TestConfiguration.StartupTimeoutSeconds));
        
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
    /// Start the React frontend in a visible terminal window.
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
        var browserEnv = "none"; // Don't open browser - Playwright controls it

        // Start frontend in a visible terminal window
        if (OperatingSystem.IsWindows())
        {
            _frontendProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k \"cd /d \"{frontendPath}\" && set PORT={port} && set BROWSER={browserEnv} && npm start\"",
                    UseShellExecute = true,
                    CreateNoWindow = false
                }
            };
        }
        else
        {
            _frontendProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GetNpmCommand(),
                    Arguments = "start",
                    WorkingDirectory = frontendPath,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    Environment =
                    {
                        ["BROWSER"] = browserEnv,
                        ["PORT"] = port.ToString()
                    }
                }
            };
        }

        _frontendProcess.Start();
        Console.WriteLine($"[E2E] Frontend terminal opened (PID: {_frontendProcess.Id})");

        // Wait for frontend to be ready
        await WaitForServiceAsync(
            TestConfiguration.BaseUrl,
            "Frontend",
            TimeSpan.FromSeconds(TestConfiguration.StartupTimeoutSeconds));
    }

    /// <summary>
    /// Wait for a service to respond.
    /// </summary>
    private async Task WaitForServiceAsync(string url, string serviceName, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var lastError = "";
        var attemptCount = 0;

        Console.WriteLine($"[E2E] Waiting for {serviceName} at {url}...");

        while (stopwatch.Elapsed < timeout)
        {
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
