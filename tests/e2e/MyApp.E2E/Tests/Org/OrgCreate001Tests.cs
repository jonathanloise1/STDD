using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using System.Text.Json;

namespace MyApp.E2E.Tests.Org;

/// <summary>
/// ORG-CREATE-001: Successfully Create Organization
///
/// Verifies that an authenticated user can create a new organization
/// via POST /api/organizations with all required fields.
/// </summary>
[TestFixture]
[Category("Org")]
[Property("TestCase", "ORG-CREATE-001")]
[Property("UserStory", "US-ORG-01")]
public class OrgCreate001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    private string? _createdOrgId;

    [Test]
    [Description("POST /api/organizations creates org and returns correct data")]
    public async Task Create_ShouldReturnNewOrganization()
    {
        var uniqueName = UniqueTestName("E2E Corp");

        var response = await Page.APIRequest.PostAsync(
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
                    vatNumber = "IT99999999999",
                    fiscalCode = "99999999999",
                    billingAddress = "Via Test 1",
                    billingCity = "Milano",
                    billingProvince = "MI",
                    billingZipCode = "20100",
                    billingCountryCode = "IT",
                    billingEmail = "billing@e2ecorp.test"
                },
                IgnoreHTTPSErrors = true
            });

        Console.WriteLine($"[ORG-CREATE-001] Create response status: {response.Status}");
        Assert.That(response.Status, Is.EqualTo(201), "POST /api/organizations should return 201 Created");

        var json = await response.JsonAsync();
        Assert.That(json, Is.Not.Null, "Response body should not be null");

        // Verify id is a valid GUID
        _createdOrgId = json?.GetProperty("id").GetString();
        Console.WriteLine($"[ORG-CREATE-001] Created org id: {_createdOrgId}");
        Assert.That(Guid.TryParse(_createdOrgId, out _), Is.True, "id should be a valid GUID");

        // Verify all fields match
        Assert.That(json?.GetProperty("name").GetString(), Is.EqualTo(uniqueName));
        Assert.That(json?.GetProperty("legalName").GetString(), Is.EqualTo($"{uniqueName} S.r.l."));
        Assert.That(json?.GetProperty("vatNumber").GetString(), Is.EqualTo("IT99999999999"));
        Assert.That(json?.GetProperty("fiscalCode").GetString(), Is.EqualTo("99999999999"));
        Assert.That(json?.GetProperty("billingAddress").GetString(), Is.EqualTo("Via Test 1"));
        Assert.That(json?.GetProperty("billingCity").GetString(), Is.EqualTo("Milano"));
        Assert.That(json?.GetProperty("billingProvince").GetString(), Is.EqualTo("MI"));
        Assert.That(json?.GetProperty("billingZipCode").GetString(), Is.EqualTo("20100"));
        Assert.That(json?.GetProperty("billingCountryCode").GetString(), Is.EqualTo("IT"));
        Assert.That(json?.GetProperty("billingEmail").GetString(), Is.EqualTo("billing@e2ecorp.test"));

        // Verify persistence via GET
        var getResponse = await Page.APIRequest.GetAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations/{_createdOrgId}",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                IgnoreHTTPSErrors = true
            });

        Assert.That(getResponse.Status, Is.EqualTo(200), "GET should return the created org");

        var getJson = await getResponse.JsonAsync();
        Assert.That(getJson?.GetProperty("name").GetString(), Is.EqualTo(uniqueName),
            "Persisted name should match");
    }

    [TearDown]
    public async Task CleanupCreatedOrg()
    {
        // Soft-delete the org created during test to keep DB clean
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
