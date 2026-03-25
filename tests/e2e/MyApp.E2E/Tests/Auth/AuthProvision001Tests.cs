using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Auth;

/// <summary>
/// AUTH-PROVISION-001: First Login Creates a New User Record
///
/// Verifies that when a "new" user syncs for the first time,
/// the backend returns 200 with an empty organizations array.
///
/// NOTE: The ImpersonationMiddleware requires the user to exist in DB,
/// so we use a pre-seeded user with zero memberships to simulate
/// a freshly provisioned user.
/// </summary>
[TestFixture]
[Category("Auth")]
[Property("TestCase", "AUTH-PROVISION-001")]
[Property("UserStory", "US-AUTH-02")]
public class AuthProvision001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.NewUser;

    [Test]
    [Description("Sync returns 200 with user data and empty organizations for a new user")]
    public async Task Sync_ShouldReturn200_WithEmptyOrganizations()
    {
        var response = await Page.APIRequest.PostAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/auth/sync",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.NewUser.Id
                },
                DataObject = new { },
                IgnoreHTTPSErrors = true
            });

        Console.WriteLine($"[AUTH-PROVISION-001] Sync response status: {response.Status}");
        Assert.That(response.Status, Is.EqualTo(200), "POST /api/auth/sync should return 200");

        var json = await response.JsonAsync();
        Assert.That(json, Is.Not.Null, "Response body should not be null");

        // Verify user data
        var id = json?.GetProperty("id").GetString();
        var email = json?.GetProperty("email").GetString();
        Console.WriteLine($"[AUTH-PROVISION-001] User id={id}, email={email}");

        Assert.That(id, Is.Not.Null.And.Not.Empty, "Should have user id");
        Assert.That(email, Is.EqualTo(TestUsers.NewUser.Email), "Email should match new user email");

        // Verify empty organizations
        var orgs = json?.GetProperty("organizations");
        Assert.That(orgs?.GetArrayLength(), Is.EqualTo(0),
            "New user should have zero organizations");
    }
}
