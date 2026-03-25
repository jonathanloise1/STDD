using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests;

/// <summary>
/// Smoke tests to verify the basic E2E infrastructure works:
/// - Frontend loads
/// - Impersonation authentication works
/// - Dashboard is accessible
/// - Basic navigation works
/// </summary>
[TestFixture]
[Category("Smoke")]
public class SmokeTests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Verify that the frontend loads and the dashboard is accessible with impersonated auth")]
    public async Task Dashboard_ShouldLoad_WhenAuthenticated()
    {
        // Navigate to root — should redirect to dashboard
        await NavigateToAsync("/");

        // Wait for page to stabilize
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The page should have loaded
        var url = Page.Url;
        Console.WriteLine($"[Test] Current URL: {url}");
        Assert.That(url, Does.Contain("localhost:3147"), "Should stay on the app");
    }

    [Test]
    [Description("Verify that the dashboard page renders with navigation elements")]
    public async Task Dashboard_ShouldShowNavigation()
    {
        await NavigateToAsync("/dashboard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The page should have a navigation/sidebar element
        // Wait for either a nav element or the aside (sidebar)
        var sidebar = Page.Locator("aside, nav").First;
        await Assertions.Expect(sidebar).ToBeVisibleAsync(
            new() { Timeout = 15000 });

        Console.WriteLine("[Test] Navigation/sidebar is visible");
    }

    [Test]
    [Description("Verify that the settings page is accessible for admin users")]
    public async Task Settings_ShouldBeAccessible_ForAdminUser()
    {
        await NavigateToAsync("/settings/organization");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should not be redirected to login or show unauthorized
        var url = Page.Url;
        Console.WriteLine($"[Test] Current URL: {url}");
        Assert.That(url, Does.Contain("settings"), "Should stay on settings page");
    }

    [Test]
    [Description("Verify API connectivity — the /api/organizations endpoint should respond")]
    public async Task Api_ShouldRespond_WithOrganizations()
    {
        // Make a direct API call with impersonation header (no page navigation needed)
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

        Console.WriteLine($"[Test] API response status: {response.Status}");
        Assert.That(response.Status, Is.EqualTo(200), "Organizations API should return 200");

        var body = await response.TextAsync();
        Console.WriteLine($"[Test] API response body length: {body.Length} chars");
        Assert.That(body, Is.Not.Empty, "Response body should not be empty");
    }
}
