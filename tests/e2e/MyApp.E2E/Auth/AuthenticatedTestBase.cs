using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Auth;

/// <summary>
/// Base class for tests that require authentication.
/// 
/// Uses E2E Impersonation pattern for authentication:
/// - Frontend: localStorage mock user → bypasses MSAL
/// - Backend: X-MyApp-Impersonate-UserId header → ImpersonationMiddleware
/// 
/// This allows tests to run without manual OTP code entry, making them
/// suitable for CI/CD pipelines.
/// 
/// For tests that need to verify the actual login flow (login/logout tests),
/// use AuthStateManager directly instead.
/// </summary>
[Parallelizable(ParallelScope.Self)]
public abstract class AuthenticatedTestBase : PageTest
{
    protected TestUser CurrentUser { get; private set; } = null!;
    protected new IPage Page { get; private set; } = null!;
    
    /// <summary>
    /// Browser context with impersonation configured.
    /// </summary>
    protected IBrowserContext AuthContext { get; private set; } = null!;
    
    /// <summary>
    /// Unique identifier for this test run.
    /// Used to identify and cleanup test data.
    /// </summary>
    protected string TestRunId { get; private set; } = null!;

    /// <summary>
    /// Override to specify which test user to authenticate as.
    /// </summary>
    protected abstract TestUser GetTestUser();

    [SetUp]
    public async Task AuthSetup()
    {
        // Unique identifier for this test run
        TestRunId = $"E2E-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{TestContext.CurrentContext.Test.ID}";
        
        CurrentUser = GetTestUser();

        // Create browser context with impersonation (no real login needed)
        AuthContext = await E2EImpersonation.CreateImpersonatedContextAsync(
            Browser,
            CurrentUser
        );

        // Create new page from impersonated context
        Page = await AuthContext.NewPageAsync();
        Page.SetDefaultTimeout(TestConfiguration.Timeout);
        
        // Bring browser to front for visibility during debugging
        await Page.BringToFrontAsync();
        
        Console.WriteLine($"[E2E] Test setup complete. User: {CurrentUser.Email}, TestId: {TestRunId}");
    }

    [TearDown]
    public async Task AuthTeardown()
    {
        // Log test result
        var testResult = TestContext.CurrentContext.Result;
        var testName = TestContext.CurrentContext.Test.Name;
        var status = testResult.Outcome.Status;
        
        if (status == NUnit.Framework.Interfaces.TestStatus.Passed)
        {
            Console.WriteLine($"[E2E] ✓ PASSED: {testName}");
        }
        else if (status == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            var errorMessage = testResult.Message?.Split('\n').FirstOrDefault() ?? "Unknown error";
            Console.WriteLine($"[E2E] ✗ FAILED: {testName}");
            Console.WriteLine($"[E2E]   Reason: {errorMessage}");
            
            // Capture screenshot on failure
            await CaptureScreenshotAsync();
        }
        else if (status == NUnit.Framework.Interfaces.TestStatus.Skipped)
        {
            Console.WriteLine($"[E2E] ⊘ SKIPPED: {testName}");
        }
        else
        {
            Console.WriteLine($"[E2E] ? {status.ToString().ToUpper()}: {testName}");
        }
        
        // Dispose context
        if (AuthContext != null)
        {
            await AuthContext.CloseAsync();
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
    /// Wait for toast message to appear.
    /// </summary>
    protected async Task WaitForToastAsync(string expectedText)
    {
        await Assertions.Expect(Page.GetByTestId("toast-message"))
            .ToContainTextAsync(expectedText);
    }

    /// <summary>
    /// Dynamically resolves the DevOrg ID by querying the organizations API
    /// and finding the org named "MyApp Dev Org". Always uses AdminUser
    /// to guarantee visibility regardless of which user the test authenticates as.
    /// </summary>
    protected async Task<string> ResolveDevOrgIdAsync()
    {
        var response = await Page.APIRequest.GetAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                IgnoreHTTPSErrors = true
            });

        Assert.That(response.Status, Is.EqualTo(200), "Should be able to list organizations");

        var json = await response.JsonAsync();
        for (int i = 0; i < json?.GetArrayLength(); i++)
        {
            var org = json.Value[i];
            if (org.GetProperty("name").GetString() == "MyApp Dev Org")
                return org.GetProperty("id").GetString()!;
        }

        Assert.Fail("DevOrg 'MyApp Dev Org' not found in organizations list");
        return null!; // unreachable
    }

    /// <summary>
    /// Capture screenshot on test failure.
    /// </summary>
    private async Task CaptureScreenshotAsync()
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
    }
}
