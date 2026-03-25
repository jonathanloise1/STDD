using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Org;

/// <summary>
/// ORG-UPDATE-001: Admin Updates Organization Settings
///
/// Verifies that an Admin can update all organization editable fields
/// via PUT /api/organizations/{id}.
/// </summary>
[TestFixture]
[Category("Org")]
[Property("TestCase", "ORG-UPDATE-001")]
[Property("UserStory", "US-ORG-04")]
public class OrgUpdate001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    // We create a dedicated org so we don't interfere with other tests that use DevOrg.
    private string? _testOrgId;

    [Test]
    [Description("Admin can update organization name and billing fields")]
    public async Task Update_ShouldModifyOrganizationFields()
    {
        // Step 1: Create a throwaway org for this test
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
                    name = UniqueTestName("Update Org"),
                    legalName = "Update Org S.r.l.",
                    vatNumber = "IT77777777777",
                    fiscalCode = "77777777777",
                    billingAddress = "Via Before 1",
                    billingCity = "Milano",
                    billingProvince = "MI",
                    billingZipCode = "20100",
                    billingCountryCode = "IT",
                    billingEmail = "before@update.test"
                },
                IgnoreHTTPSErrors = true
            });

        Assert.That(createResponse.Status, Is.EqualTo(201));
        var createJson = await createResponse.JsonAsync();
        _testOrgId = createJson?.GetProperty("id").GetString();

        // Step 2: Update the organization
        var updatedName = UniqueTestName("Updated Org");
        var updateResponse = await Page.APIRequest.PutAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations/{_testOrgId}",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                DataObject = new
                {
                    name = updatedName,
                    legalName = "Updated Org S.p.A.",
                    vatNumber = "IT77777777777",
                    fiscalCode = "77777777777",
                    billingAddress = "Via After 10",
                    billingCity = "Roma",
                    billingProvince = "RM",
                    billingZipCode = "00100",
                    billingCountryCode = "IT",
                    billingEmail = "after@update.test"
                },
                IgnoreHTTPSErrors = true
            });

        Console.WriteLine($"[ORG-UPDATE-001] Update response status: {updateResponse.Status}");
        Assert.That(updateResponse.Status, Is.EqualTo(200), "PUT should return 200 OK");

        var updateJson = await updateResponse.JsonAsync();
        Assert.That(updateJson?.GetProperty("name").GetString(), Is.EqualTo(updatedName));
        Assert.That(updateJson?.GetProperty("legalName").GetString(), Is.EqualTo("Updated Org S.p.A."));
        Assert.That(updateJson?.GetProperty("billingCity").GetString(), Is.EqualTo("Roma"));
        Assert.That(updateJson?.GetProperty("billingEmail").GetString(), Is.EqualTo("after@update.test"));

        // Step 3: Confirm persistence via GET
        var getResponse = await Page.APIRequest.GetAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations/{_testOrgId}",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                IgnoreHTTPSErrors = true
            });

        Assert.That(getResponse.Status, Is.EqualTo(200));
        var getJson = await getResponse.JsonAsync();
        Assert.That(getJson?.GetProperty("name").GetString(), Is.EqualTo(updatedName),
            "GET should reflect the updated name");
    }

    [TearDown]
    public async Task CleanupTestOrg()
    {
        if (_testOrgId is not null)
        {
            await Page.APIRequest.DeleteAsync(
                $"{TestConfiguration.ApiBaseUrl}/api/organizations/{_testOrgId}",
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
