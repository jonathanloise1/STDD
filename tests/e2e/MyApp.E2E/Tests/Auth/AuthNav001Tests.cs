using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Auth;

/// <summary>
/// AUTH-NAV-001: User With Organizations Redirected to Dashboard
///
/// Verifies that after login, a user who belongs to at least one
/// organization is redirected to /dashboard.
/// </summary>
[TestFixture]
[Category("Auth")]
[Property("TestCase", "AUTH-NAV-001")]
[Property("UserStory", "US-AUTH-07")]
public class AuthNav001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Navigating to root redirects authenticated user to /dashboard")]
    public async Task Root_ShouldRedirectToDashboard()
    {
        // Navigate to root
        await NavigateToAsync("/");

        // Wait for redirects to settle
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var url = Page.Url;
        Console.WriteLine($"[AUTH-NAV-001] URL after navigating to /: {url}");

        Assert.That(url, Does.Contain("/dashboard"),
            "User with organizations should be redirected to /dashboard");
    }

    [Test]
    [Description("Dashboard content is visible after redirect")]
    public async Task Dashboard_ShouldRenderContent()
    {
        await NavigateToAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Verify dashboard has rendered meaningful content (sidebar/nav)
        var sidebar = Page.Locator("aside, nav").First;
        await Assertions.Expect(sidebar).ToBeVisibleAsync(
            new() { Timeout = 15000 });

        Console.WriteLine("[AUTH-NAV-001] Dashboard content rendered successfully");
    }

    [Test]
    [Description("No redirect loops detected")]
    public async Task Navigation_ShouldNotLoop()
    {
        var navigationCount = 0;

        Page.Response += (_, response) =>
        {
            if (response.Status is >= 300 and < 400)
                navigationCount++;
        };

        await NavigateToAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Console.WriteLine($"[AUTH-NAV-001] Redirect count: {navigationCount}");
        Assert.That(navigationCount, Is.LessThan(5),
            "Should not have excessive redirects (possible redirect loop)");
    }
}
