using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Auth;

/// <summary>
/// AUTH-LOGIN-001: Successful Login via Email + OTP (Impersonation)
///
/// Verifies that login via the impersonation pattern works and
/// the frontend displays the authenticated state.
/// </summary>
[TestFixture]
[Category("Auth")]
[Property("TestCase", "AUTH-LOGIN-001")]
[Property("UserStory", "US-AUTH-01")]
public class AuthLogin001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Protected page renders successfully after impersonation login")]
    public async Task Dashboard_ShouldLoad_WithImpersonation()
    {
        // Navigate to root — should redirect to dashboard
        await NavigateToAsync("/");

        // Should stay on the app — not redirected to external login
        var url = Page.Url;
        Console.WriteLine($"[AUTH-LOGIN-001] Current URL: {url}");
        Assert.That(url, Does.Contain("localhost"), "Should stay on the app, not redirect to external login");

        // Should land on /dashboard
        Assert.That(url, Does.Contain("/dashboard"), "Should be redirected to dashboard");
    }

    [Test]
    [Description("User identity is visible in the UI after login")]
    public async Task Dashboard_ShouldShowUserIdentity()
    {
        await NavigateToAsync("/dashboard");

        // Wait for dashboard to fully render
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // User name or email should be visible somewhere in the header/sidebar
        // Look for common patterns: avatar, email text, user name
        var bodyText = await Page.Locator("body").InnerTextAsync();
        Console.WriteLine($"[AUTH-LOGIN-001] Looking for user identity on page");

        // The admin user's name or email should appear in the UI
        var hasUserIdentity = bodyText.Contains("Dev", StringComparison.OrdinalIgnoreCase)
                              || bodyText.Contains(TestUsers.AdminUser.Email, StringComparison.OrdinalIgnoreCase);

        Assert.That(hasUserIdentity, Is.True,
            "User name or email should be visible in the UI");
    }

    [Test]
    [Description("POST /api/auth/sync returns 200 with user data and organizations")]
    public async Task Sync_ShouldReturn200_WithUserData()
    {
        var response = await Page.APIRequest.PostAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/auth/sync",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                DataObject = new { },
                IgnoreHTTPSErrors = true
            });

        Console.WriteLine($"[AUTH-LOGIN-001] Sync response status: {response.Status}");
        Assert.That(response.Status, Is.EqualTo(200), "POST /api/auth/sync should return 200");

        var json = await response.JsonAsync();
        Assert.That(json, Is.Not.Null, "Response body should not be null");

        // Verify response has id, email, organizations
        Assert.That(json?.GetProperty("id").GetString(), Is.Not.Null.And.Not.Empty, "Should have user id");
        Assert.That(json?.GetProperty("email").GetString(), Is.EqualTo(TestUsers.AdminUser.Email),
            "Email should match test user");

        var orgs = json?.GetProperty("organizations");
        Assert.That(orgs?.GetArrayLength(), Is.GreaterThan(0), "Should have at least one organization");
    }
}
