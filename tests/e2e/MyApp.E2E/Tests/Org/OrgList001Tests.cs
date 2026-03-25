using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Org;

/// <summary>
/// ORG-LIST-001: List Returns User's Active Organizations
///
/// Verifies that GET /api/organizations returns all non-deleted
/// organizations where the user is an active member.
/// </summary>
[TestFixture]
[Category("Org")]
[Property("TestCase", "ORG-LIST-001")]
[Property("UserStory", "US-ORG-02")]
public class OrgList001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("GET /api/organizations returns user's active organizations with correct structure")]
    public async Task List_ShouldReturnActiveOrganizations()
    {
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

        Console.WriteLine($"[ORG-LIST-001] Response status: {response.Status}");
        Assert.That(response.Status, Is.EqualTo(200), "GET /api/organizations should return 200");

        var json = await response.JsonAsync();
        Assert.That(json, Is.Not.Null);

        var orgs = json?.EnumerateArray().ToList();
        Console.WriteLine($"[ORG-LIST-001] Organizations count: {orgs?.Count}");
        Assert.That(orgs, Has.Count.GreaterThan(0), "Should have at least one organization");

        // Find "MyApp Dev Org" (from seed data)
        var devOrg = orgs?.FirstOrDefault(o =>
            o.GetProperty("name").GetString() == "MyApp Dev Org");

        Assert.That(devOrg, Is.Not.Null, "Should include 'MyApp Dev Org' from seed data");

        // Verify structure of each organization
        foreach (var org in orgs!)
        {
            var id = org.GetProperty("id").GetString();
            var name = org.GetProperty("name").GetString();
            var legalName = org.GetProperty("legalName").GetString();

            Console.WriteLine($"[ORG-LIST-001] Org: id={id}, name={name}");

            Assert.That(Guid.TryParse(id, out _), Is.True, $"id should be a valid GUID for org '{name}'");
            Assert.That(name, Is.Not.Null.And.Not.Empty, "name should not be empty");
            Assert.That(legalName, Is.Not.Null.And.Not.Empty, "legalName should not be empty");

            // Verify users array exists
            var users = org.GetProperty("users");
            Assert.That(users.GetArrayLength(), Is.GreaterThan(0),
                $"Org '{name}' should have at least one user");
        }
    }
}
