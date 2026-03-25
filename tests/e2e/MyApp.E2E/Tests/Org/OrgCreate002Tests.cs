using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Org;

/// <summary>
/// ORG-CREATE-002: Creator Is Added as Admin
///
/// Verifies that the user who creates an organization is automatically
/// added as an Admin with Active status.
/// </summary>
[TestFixture]
[Category("Org")]
[Property("TestCase", "ORG-CREATE-002")]
[Property("UserStory", "US-ORG-01")]
public class OrgCreate002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    private string? _createdOrgId;

    [Test]
    [Description("Creator is automatically added as Admin with Active status")]
    public async Task Creator_ShouldBeAdminWithActiveStatus()
    {
        var uniqueName = UniqueTestName("Creator Org");

        // Step 1: Create organization
        var createResponse = await Page.APIRequest.PostAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                DataObject = new
                {
                    name = uniqueName,
                    legalName = $"{uniqueName} S.r.l.",
                    vatNumber = "IT88888888888",
                    fiscalCode = "88888888888",
                    billingAddress = "Via Creator 1",
                    billingCity = "Roma",
                    billingProvince = "RM",
                    billingZipCode = "00100",
                    billingCountryCode = "IT",
                    billingEmail = "creator@e2ecorp.test"
                },
                IgnoreHTTPSErrors = true
            });

        Assert.That(createResponse.Status, Is.EqualTo(201), "Should create org successfully");

        var createJson = await createResponse.JsonAsync();
        _createdOrgId = createJson?.GetProperty("id").GetString();
        Console.WriteLine($"[ORG-CREATE-002] Created org id: {_createdOrgId}");

        // Step 2: Get organization users
        var usersResponse = await Page.APIRequest.GetAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations/{_createdOrgId}/users",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                IgnoreHTTPSErrors = true
            });

        Assert.That(usersResponse.Status, Is.EqualTo(200), "Should return users list");

        var usersJson = await usersResponse.JsonAsync();
        Assert.That(usersJson, Is.Not.Null);

        var usersArray = usersJson?.EnumerateArray().ToList();
        Console.WriteLine($"[ORG-CREATE-002] Users count: {usersArray?.Count}");
        Assert.That(usersArray, Has.Count.EqualTo(1), "Should have exactly one member (the creator)");

        // Step 3: Verify the creator is Admin + Active
        var creator = usersArray![0];
        var role = creator.GetProperty("role").GetString();
        var status = creator.GetProperty("status").GetString();
        var email = creator.GetProperty("email").GetString();

        Console.WriteLine($"[ORG-CREATE-002] Creator: email={email}, role={role}, status={status}");

        Assert.That(role, Is.EqualTo("Admin"), "Creator should have Admin role");
        Assert.That(status, Is.EqualTo("Active"), "Creator should have Active status");
        Assert.That(email, Is.EqualTo(TestUsers.AdminUser.Email), "Creator email should match");
    }

    [TearDown]
    public async Task CleanupCreatedOrg()
    {
        if (_createdOrgId is not null)
        {
            await Page.APIRequest.DeleteAsync(
                $"{TestConfiguration.ApiBaseUrl}/api/organizations/{_createdOrgId}",
                new()
                {
                    Headers = new Dictionary<string, string>
                    {
                        [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                    },
                    IgnoreHTTPSErrors = true
                });
        }
    }
}
