using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Auth;

/// <summary>
/// AUTH-SYNC-001: Sync Returns User ID, Email, and Organizations
///
/// Verifies the structure and content of the POST /api/auth/sync response
/// for an admin user with active organization memberships.
/// </summary>
[TestFixture]
[Category("Auth")]
[Property("TestCase", "AUTH-SYNC-001")]
[Property("UserStory", "US-AUTH-04")]
public class AuthSync001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Sync returns correct user data with id, email, and organizations")]
    public async Task Sync_ShouldReturnCorrectUserData()
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

        Console.WriteLine($"[AUTH-SYNC-001] Sync response status: {response.Status}");
        Assert.That(response.Status, Is.EqualTo(200), "POST /api/auth/sync should return 200");

        var json = await response.JsonAsync();
        Assert.That(json, Is.Not.Null, "Response body should not be null");

        // Verify id is a valid GUID
        var id = json?.GetProperty("id").GetString();
        Console.WriteLine($"[AUTH-SYNC-001] User id: {id}");
        Assert.That(Guid.TryParse(id, out _), Is.True, "id should be a valid GUID");

        // Verify email matches
        var email = json?.GetProperty("email").GetString();
        Console.WriteLine($"[AUTH-SYNC-001] User email: {email}");
        Assert.That(email, Is.EqualTo(TestUsers.AdminUser.Email), "Email should match admin user");

        // Verify organizations array
        var orgs = json?.GetProperty("organizations");
        Assert.That(orgs, Is.Not.Null, "Should have organizations property");
        Assert.That(orgs?.GetArrayLength(), Is.GreaterThan(0), "Should have at least one organization");

        // Verify first organization structure
        var firstOrg = orgs?[0];
        var orgId = firstOrg?.GetProperty("organizationId").GetString();
        var orgName = firstOrg?.GetProperty("organizationName").GetString();
        var role = firstOrg?.GetProperty("role").GetString();

        Console.WriteLine($"[AUTH-SYNC-001] Org: id={orgId}, name={orgName}, role={role}");

        Assert.That(Guid.TryParse(orgId, out _), Is.True, "organizationId should be a valid GUID");
        Assert.That(orgName, Is.Not.Null.And.Not.Empty, "organizationName should not be empty");
        Assert.That(role, Is.EqualTo("Admin"), "Admin user should have Admin role");
    }
}
