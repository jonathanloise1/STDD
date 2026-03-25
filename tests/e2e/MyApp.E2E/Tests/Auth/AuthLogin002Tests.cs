using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Auth;

/// <summary>
/// AUTH-LOGIN-002: Unauthorized Access Without Authentication
///
/// Verifies that unauthenticated requests to protected API endpoints
/// return 401 Unauthorized.
/// </summary>
[TestFixture]
[Category("Auth")]
[Property("TestCase", "AUTH-LOGIN-002")]
[Property("UserStory", "US-AUTH-01")]
public class AuthLogin002Tests : PageTest
{
    [Test]
    [Description("POST /api/auth/sync without auth returns 401")]
    public async Task Sync_ShouldReturn401_WithoutAuth()
    {
        var request = await Playwright.APIRequest.NewContextAsync(new()
        {
            IgnoreHTTPSErrors = true
        });

        var response = await request.PostAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/auth/sync",
            new() { DataObject = new { } });

        Console.WriteLine($"[AUTH-LOGIN-002] Sync without auth → {response.Status}");
        Assert.That(response.Status, Is.EqualTo(401), "Sync without auth should return 401");

        await request.DisposeAsync();
    }

    [Test]
    [Description("GET /api/organizations without auth returns 401")]
    public async Task Organizations_ShouldReturn401_WithoutAuth()
    {
        var request = await Playwright.APIRequest.NewContextAsync(new()
        {
            IgnoreHTTPSErrors = true
        });

        var response = await request.GetAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations");

        Console.WriteLine($"[AUTH-LOGIN-002] Organizations without auth → {response.Status}");
        Assert.That(response.Status, Is.EqualTo(401), "Organizations without auth should return 401");

        // Verify no user data is leaked
        var body = await response.TextAsync();
        Assert.That(body, Does.Not.Contain("email"), "Response should not leak user data");
        Assert.That(body, Does.Not.Contain("@"), "Response should not leak email addresses");

        await request.DisposeAsync();
    }
}
