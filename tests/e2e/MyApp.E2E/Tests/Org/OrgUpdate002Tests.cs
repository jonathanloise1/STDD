using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Org;

/// <summary>
/// ORG-UPDATE-002: Non-Admin Cannot Update Organization
///
/// Verifies that a user with Editor role receives 403 Forbidden
/// when trying to update organization settings.
/// </summary>
[TestFixture]
[Category("Org")]
[Property("TestCase", "ORG-UPDATE-002")]
[Property("UserStory", "US-ORG-04")]
public class OrgUpdate002Tests : AuthenticatedTestBase
{
    // Authenticate as the Editor (MemberUser)
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("Editor should receive 403 when updating organization")]
    public async Task Update_AsEditor_ShouldReturn403()
    {
        var devOrgId = await ResolveDevOrgIdAsync();

        var updateResponse = await Page.APIRequest.PutAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations/{devOrgId}",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.MemberUser.Id
                },
                DataObject = new
                {
                    name = "Should Be Forbidden",
                    legalName = "Forbidden S.r.l.",
                    vatNumber = "IT99999999999",
                    fiscalCode = "99999999999",
                    billingAddress = "Via Forbidden 1",
                    billingCity = "Milano",
                    billingProvince = "MI",
                    billingZipCode = "20100",
                    billingCountryCode = "IT",
                    billingEmail = "forbidden@update.test"
                },
                IgnoreHTTPSErrors = true
            });

        Console.WriteLine($"[ORG-UPDATE-002] Update response status: {updateResponse.Status}");
        Assert.That(updateResponse.Status, Is.EqualTo(403),
            "Editor should receive 403 Forbidden when updating organization");
    }

    [Test]
    [Description("Original organization data should remain unchanged after forbidden update attempt")]
    public async Task Update_AsEditor_ShouldNotModifyData()
    {
        var devOrgId = await ResolveDevOrgIdAsync();

        // First, attempt the forbidden update
        await Page.APIRequest.PutAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations/{devOrgId}",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.MemberUser.Id
                },
                DataObject = new
                {
                    name = "Tampered Name",
                    legalName = "Tampered S.r.l.",
                    vatNumber = "IT00000000000",
                    fiscalCode = "00000000000",
                    billingAddress = "Via Tampered",
                    billingCity = "Nowhere",
                    billingProvince = "XX",
                    billingZipCode = "00000",
                    billingCountryCode = "IT",
                    billingEmail = "tampered@test.test"
                },
                IgnoreHTTPSErrors = true
            });

        // Then, verify the org hasn't changed (use Admin to GET)
        var getResponse = await Page.APIRequest.GetAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations/{devOrgId}",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                IgnoreHTTPSErrors = true
            });

        Assert.That(getResponse.Status, Is.EqualTo(200));
        var json = await getResponse.JsonAsync();
        Assert.That(json?.GetProperty("name").GetString(), Is.EqualTo("MyApp Dev Org"),
            "Organization name should not have been modified by Editor");
    }
}
