using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E;

/// <summary>
/// Global setup and teardown for all E2E tests.
/// 
/// AUTO-START APPLICATIONS:
/// By default, AutoStartApps is set to FALSE in appsettings.e2e.json.
/// This expects you to manually start frontend (npm start) and backend (dotnet run) before running tests.
/// 
/// To enable auto-start:
/// 1. Set "AutoStartApps": true in appsettings.e2e.json
/// 2. Ensure backend has valid configuration (database, Azure AD, secrets)
/// 3. Frontend will use npm start
/// 
/// The applications paths are auto-detected from the workspace root.
/// Override with environment variables E2E_BACKEND_PATH and E2E_FRONTEND_PATH if needed.
/// </summary>
[SetUpFixture]
public class GlobalSetup
{
    [OneTimeSetUp]
    public async Task GlobalOneTimeSetUp()
    {
        // Start the global timer for the summary
        TestResultTracker.Instance.StartTimer();
        
        if (TestConfiguration.AutoStartApps)
        {
            TestContext.Progress.WriteLine("=== Starting applications for E2E tests ===");
            TestContext.Progress.WriteLine($"Backend path: {TestConfiguration.BackendPath}");
            TestContext.Progress.WriteLine($"Frontend path: {TestConfiguration.FrontendPath}");
            
            try
            {
                await ApplicationManager.Instance.StartApplicationsAsync();
                TestContext.Progress.WriteLine("=== Applications started successfully ===");
            }
            catch (Exception ex)
            {
                TestContext.Progress.WriteLine($"=== FAILED to start applications: {ex.Message} ===");
                throw;
            }
        }
        else
        {
            TestContext.Progress.WriteLine("=== AutoStartApps is disabled, expecting apps to be running manually ===");
        }

        // Warmup: trigger Vite dev server compilation before tests start.
        // The first browser navigation compiles all modules; subsequent loads are fast.
        await WarmupFrontendAsync();
    }

    private async Task WarmupFrontendAsync()
    {
        TestContext.Progress.WriteLine("=== Warming up frontend (Vite compilation) ===");
        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync();
            await page.GotoAsync(TestConfiguration.BaseUrl, new() { Timeout = 120_000 });
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            TestContext.Progress.WriteLine("=== Frontend warmup complete ===");
        }
        catch (Exception ex)
        {
            TestContext.Progress.WriteLine($"=== Frontend warmup failed (non-fatal): {ex.Message} ===");
        }
    }

    [OneTimeTearDown]
    public async Task GlobalOneTimeTearDown()
    {
        // Print the final test run summary
        var summary = TestResultTracker.Instance.BuildSummary();
        TestContext.Progress.WriteLine(summary);
        Console.WriteLine(summary);
        
        // Export to files (JSON + TXT)
        var outputDir = TestResultTracker.Instance.ExportToFiles();
        TestContext.Progress.WriteLine($"=== Summary exported to: {outputDir} ===");
        
        if (TestConfiguration.AutoStartApps && TestConfiguration.StopAppsAfterTests)
        {
            TestContext.Progress.WriteLine("=== Stopping applications ===");
            await ApplicationManager.Instance.DisposeAsync();
            TestContext.Progress.WriteLine("=== Applications stopped ===");
        }
    }
}
