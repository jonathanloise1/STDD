using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace MyApp.E2E.Infrastructure;

/// <summary>
/// Base class for all E2E tests.
/// Provides common utilities and test isolation.
/// </summary>
[Parallelizable(ParallelScope.Self)]
public abstract class TestBase : PageTest
{
    /// <summary>
    /// Unique identifier for this test run.
    /// Used to identify and cleanup test data.
    /// </summary>
    protected string TestRunId { get; private set; } = null!;

    [SetUp]
    public virtual async Task BaseSetup()
    {
        // Unique identifier for this test run
        TestRunId = $"E2E-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{TestContext.CurrentContext.Test.ID}";
        
        // Log test start with clear visual separator
        var testName = TestContext.CurrentContext.Test.FullName;
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║ ▶ STARTING: {testName}");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        
        // Set default timeout
        Page.SetDefaultTimeout(TestConfiguration.Timeout);
        await Task.CompletedTask;
    }

    [TearDown]
    public virtual async Task BaseTeardown()
    {
        var testName = TestContext.CurrentContext.Test.Name;
        var fullName = TestContext.CurrentContext.Test.FullName;
        var status = TestContext.CurrentContext.Result.Outcome.Status;
        var statusIcon = status == NUnit.Framework.Interfaces.TestStatus.Passed ? "✓" : "✗";
        
        Console.WriteLine();
        Console.WriteLine($"║ {statusIcon} FINISHED: {testName} - {status}");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        
        // Capture screenshot on failure
        string? screenshotPath = null;
        if (status == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            screenshotPath = await CaptureScreenshotAsync();
        }
        
        // Record result for final summary
        TestResultTracker.Instance.Record(
            fullName,
            status.ToString(),
            TestContext.CurrentContext.Result.Message,
            screenshotPath);
        
        // In debug mode, pause briefly so you can see the final state
        if (TestConfiguration.IsDebugMode)
        {
            Console.WriteLine("[DEBUG] Pausing 2 seconds before closing browser...");
            await Task.Delay(2000);
        }
    }

    /// <summary>
    /// Navigate to a path relative to the base URL.
    /// </summary>
    protected async Task NavigateToAsync(string path)
    {
        var url = $"{TestConfiguration.BaseUrl}{path}";
        await Page.GotoAsync(url);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Generates a unique name for test entities.
    /// </summary>
    protected string UniqueTestName(string prefix) 
        => $"{prefix} [{TestRunId}]";

    /// <summary>
    /// Capture screenshot on test failure.
    /// Returns the screenshot file path.
    /// </summary>
    private async Task<string?> CaptureScreenshotAsync()
    {
        try
        {
            var screenshotDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screenshots");
            Directory.CreateDirectory(screenshotDir);

            var screenshotPath = Path.Combine(
                screenshotDir,
                $"{TestContext.CurrentContext.Test.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png"
            );

            await Page.ScreenshotAsync(new PageScreenshotOptions 
            { 
                Path = screenshotPath, 
                FullPage = true 
            });

            TestContext.AddTestAttachment(screenshotPath);
            return screenshotPath;
        }
        catch
        {
            return null;
        }
    }
}
