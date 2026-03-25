using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Auth;

/// <summary>
/// AUTH-INVITE-001: Pending Membership Auto-Activated on Login
///
/// Verifies that when User C (who has a pending invitation) syncs,
/// the membership is auto-activated and the organization appears
/// in the sync response.
/// </summary>
[TestFixture]
[Category("Auth")]
[Property("TestCase", "AUTH-INVITE-001")]
[Property("UserStory", "US-AUTH-05")]
public class AuthInvite001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.UserC;

    [Test]
    [Description("Sync auto-activates pending invitation and returns the organization")]
    public async Task Sync_ShouldAutoActivatePendingInvitation()
    {
        var response = await Page.APIRequest.PostAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/auth/sync",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.UserC.Id
                },
                DataObject = new { },
                IgnoreHTTPSErrors = true
            });

        Console.WriteLine($"[AUTH-INVITE-001] Sync response status: {response.Status}");
        Assert.That(response.Status, Is.EqualTo(200), "POST /api/auth/sync should return 200");

        var json = await response.JsonAsync();
        Assert.That(json, Is.Not.Null, "Response body should not be null");

        // Verify user data
        var email = json?.GetProperty("email").GetString();
        Assert.That(email, Is.EqualTo(TestUsers.UserC.Email), "Email should match User C");

        // Verify that the pending invitation was auto-activated
        var orgs = json?.GetProperty("organizations");
        Assert.That(orgs, Is.Not.Null, "Should have organizations property");
        Assert.That(orgs?.GetArrayLength(), Is.GreaterThan(0),
            "User C should now have at least one organization after auto-activation");

        // Find "E2E Invited Org" in the organizations
        var foundInvitedOrg = false;
        for (var i = 0; i < orgs?.GetArrayLength(); i++)
        {
            var org = orgs?[i];
            var orgName = org?.GetProperty("organizationName").GetString();
            if (orgName == "E2E Invited Org")
            {
                foundInvitedOrg = true;
                var role = org?.GetProperty("role").GetString();
                Console.WriteLine($"[AUTH-INVITE-001] Found invited org with role: {role}");
                Assert.That(role, Is.EqualTo("Editor"),
                    "Auto-activated membership should have the originally invited role");
                break;
            }
        }

        Assert.That(foundInvitedOrg, Is.True,
            "E2E Invited Org should appear in organizations after auto-activation");
    }
}
