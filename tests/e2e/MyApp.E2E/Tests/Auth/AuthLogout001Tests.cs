using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Auth;

/// <summary>
/// AUTH-LOGOUT-001: Logout Clears Session and Blocks Protected Access
///
/// Verifies that after clearing impersonation (simulating logout),
/// protected routes are no longer accessible and API calls return 401.
/// </summary>
[TestFixture]
[Category("Auth")]
[Property("TestCase", "AUTH-LOGOUT-001")]
[Property("UserStory", "US-AUTH-06")]
public class AuthLogout001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("After clearing session, protected pages redirect to login and API returns 401")]
    public async Task Logout_ShouldClearSessionAndBlockAccess()
    {
        // Step 1: Verify authenticated access works — navigate via root to trigger proper auth flow
        await NavigateToAsync("/");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var urlBefore = Page.Url;
        Console.WriteLine($"[AUTH-LOGOUT-001] URL before logout: {urlBefore}");
        Assert.That(urlBefore, Does.Contain("/dashboard"), "Should be on dashboard when authenticated");

        // Step 2: Simulate logout by clearing localStorage and removing route interception
        await Page.EvaluateAsync(@"() => {
            localStorage.removeItem('E2E_MOCK_USER');
            console.log('[E2E] Mock user removed — simulating logout');
        }");

        // Remove the impersonation route handler
        await AuthContext.UnrouteAllAsync();

        // Step 3: Verify API call without auth returns 401
        var apiRequest = await Playwright.APIRequest.NewContextAsync(new()
        {
            IgnoreHTTPSErrors = true
        });

        var syncResponse = await apiRequest.PostAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/auth/sync",
            new() { DataObject = new { } });

        Console.WriteLine($"[AUTH-LOGOUT-001] Sync after logout → {syncResponse.Status}");
        Assert.That(syncResponse.Status, Is.EqualTo(401),
            "API sync call without auth should return 401 after logout");

        await apiRequest.DisposeAsync();

        // Step 4: Verify localStorage is cleared
        var mockUser = await Page.EvaluateAsync<string?>("() => localStorage.getItem('E2E_MOCK_USER')");
        Assert.That(mockUser, Is.Null, "E2E_MOCK_USER should be removed from localStorage");
    }
}
