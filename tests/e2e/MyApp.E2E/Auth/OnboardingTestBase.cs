using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Auth;

/// <summary>
/// Base class for onboarding E2E tests.
/// Same as AuthenticatedTestBase but does NOT set the onboarding localStorage bypass,
/// so the onboarding wizard is shown for new users.
/// </summary>
[Parallelizable(ParallelScope.Self)]
public abstract class OnboardingTestBase : PageTest
{
    protected TestUser CurrentUser { get; private set; } = null!;
    protected new IPage Page { get; private set; } = null!;
    protected IBrowserContext AuthContext { get; private set; } = null!;
    protected string TestRunId { get; private set; } = null!;

    protected abstract TestUser GetTestUser();

    [SetUp]
    public async Task OnboardingSetup()
    {
        TestRunId = $"E2E-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{TestContext.CurrentContext.Test.ID}";
        CurrentUser = GetTestUser();

        // US-ONBOARD-01: Create context WITHOUT onboarding bypass
        AuthContext = await E2EImpersonation.CreateImpersonatedContextAsync(
            Browser,
            CurrentUser,
            skipOnboardingBypass: true
        );

        Page = await AuthContext.NewPageAsync();
        Page.SetDefaultTimeout(TestConfiguration.Timeout);
        await Page.BringToFrontAsync();

        // Reset NewUser's onboarding state before each test for isolation
        await ResetOnboardingStateAsync();

        Console.WriteLine($"[E2E] Onboarding test setup (no onboarding bypass). User: {CurrentUser.Email}");
    }

    [TearDown]
    public async Task OnboardingTeardown()
    {
        var testResult = TestContext.CurrentContext.Result;
        var testName = TestContext.CurrentContext.Test.Name;
        var status = testResult.Outcome.Status;

        if (status == NUnit.Framework.Interfaces.TestStatus.Passed)
            Console.WriteLine($"[E2E] ✓ PASSED: {testName}");
        else if (status == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            Console.WriteLine($"[E2E] ✗ FAILED: {testName}");
            Console.WriteLine($"[E2E]   Reason: {testResult.Message?.Split('\n').FirstOrDefault() ?? "Unknown"}");
            await CaptureScreenshotAsync();
        }

        if (AuthContext != null)
            await AuthContext.CloseAsync();
    }

    protected async Task NavigateToAsync(string path)
    {
        var url = $"{TestConfiguration.BaseUrl}{path}";
        await Page.GotoAsync(url);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Resets the NewUser's onboarding state via API so each test starts clean.
    /// Calls DELETE /api/users/me/complete-onboarding → sets HasCompletedOnboarding=false, language=de.
    /// </summary>
    protected async Task ResetOnboardingStateAsync()
    {
        var response = await Page.APIRequest.DeleteAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/users/me/complete-onboarding",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = CurrentUser.Id
                },
                IgnoreHTTPSErrors = true
            });

        Console.WriteLine($"[E2E] Onboarding reset: {response.Status}");
    }

    private async Task CaptureScreenshotAsync()
    {
        var screenshotDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screenshots");
        Directory.CreateDirectory(screenshotDir);
        var screenshotPath = Path.Combine(screenshotDir,
            $"{TestContext.CurrentContext.Test.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
        TestContext.AddTestAttachment(screenshotPath);
    }
}
