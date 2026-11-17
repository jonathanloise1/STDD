using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E;

/// <summary>
/// Global setup and teardown for all E2E tests.
///
/// AUTO-START APPLICATIONS (E2E:AutoStartApps in appsettings.e2e.json, or E2E_AUTO_START_APPS):
/// - true: the backend starts in the Development environment with the fault switch enabled, on the dedicated
///   E2E database, which it drops, migrates and seeds on its way up (development.md 10.3, 16); the frontend starts
///   with npm start and without a mock user. The test project itself never touches a database.
/// - false: the applications must already run, the backend in Development with
///   MyApp__Testing__FaultInjection__Enabled=true, on a database that was reset by hand (see the README).
///
/// The paths of the applications are detected from the repository root.
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
