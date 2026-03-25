using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Onboarding;

/// <summary>
/// ONBOARD-SKIP-001: Skip onboarding redirects to dashboard.
///
/// Verifies that a first-time user can skip the onboarding wizard
/// and land on the dashboard, with the wizard not reappearing.
///
/// <userstory ref="US-ONBOARD-02" />
/// </summary>
[TestFixture]
[Category("Onboarding")]
[Property("TestCase", "ONBOARD-SKIP-001")]
[Property("UserStory", "US-ONBOARD-02")]
public class OnboardSkip001Tests : OnboardingTestBase
{
    // US-ONBOARD-02: Use NewUser who has HasCompletedOnboarding = false
    protected override TestUser GetTestUser() => TestUsers.NewUser;

    private Dictionary<string, string> ImpersonationHeaders(string userId) =>
        new() { [TestConfiguration.ImpersonationHeaderName] = userId };

    /// <summary>
    /// US-ONBOARD-02: New user skips wizard → lands on dashboard → wizard doesn't reappear.
    /// </summary>
    [Test]
    [Description("US-ONBOARD-02: Skip onboarding redirects to dashboard")]
    public async Task SkipOnboarding_RedirectsToDashboard()
    {
        // ── Step 1: Navigate to dashboard — should redirect to onboarding ──
        await NavigateToAsync("/");
        
        // Wait for the onboarding wizard to appear
        var wizard = Page.Locator("#onboarding-wizard");
        await Assertions.Expect(wizard).ToBeVisibleAsync();
        Console.WriteLine("[ONBOARD-SKIP-001] Wizard is visible");

        // ── Step 2: Click "Skip for now" ──
        var skipButton = Page.Locator("#onboarding-skip");
        await Assertions.Expect(skipButton).ToBeVisibleAsync();
        await skipButton.ClickAsync();
        Console.WriteLine("[ONBOARD-SKIP-001] Skip button clicked");

        // ── Step 3: Verify redirect away from onboarding ──
        // Wait for skip handler to complete the API call and navigate
        await Page.WaitForURLAsync(url => !url.Contains("/onboarding"), new() { Timeout = 15000 });
        Console.WriteLine("[ONBOARD-SKIP-001] Left onboarding page after skip");

        // ── Step 4: Verify wizard does NOT appear again ──
        await Page.GotoAsync($"{TestConfiguration.BaseUrl}/", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 15000 });
        await Task.Delay(2000);
        var wizardAfterSkip = Page.Locator("#onboarding-wizard");
        await Assertions.Expect(wizardAfterSkip).Not.ToBeVisibleAsync(new() { Timeout = 10000 });
        Assert.That(Page.Url, Does.Not.Contain("/onboarding"), "Should not be redirected back to onboarding");
        Console.WriteLine("[ONBOARD-SKIP-001] Wizard did not reappear");

        // ── Step 5: Verify onboarding was marked complete in backend ──
        var syncResponse = await Page.APIRequest.PostAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/auth/sync",
            new()
            {
                Headers = ImpersonationHeaders(TestUsers.NewUser.Id),
                DataObject = new { },
                IgnoreHTTPSErrors = true
            });
        Assert.That(syncResponse.Status, Is.EqualTo(200));
        var syncJson = await syncResponse.JsonAsync();
        var hasCompleted = syncJson?.GetProperty("hasCompletedOnboarding").GetBoolean();
        Assert.That(hasCompleted, Is.True, "HasCompletedOnboarding should be true after skip");
        Console.WriteLine("[ONBOARD-SKIP-001] Backend onboarding state confirmed: completed");

        // ── Step 6: Verify language remains default (de) ──
        var savedLang = syncJson?.GetProperty("preferredLanguage").GetString();
        Assert.That(savedLang, Is.EqualTo("de"), "Language should remain default (de) after skip");
        Console.WriteLine($"[ONBOARD-SKIP-001] Language confirmed: {savedLang} (default)");
    }
}
