using System.Text.Json;
using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-KPI-001: Dashboard loads for authenticated user.
///
/// <userstory ref="US-DASH-01" />
/// </summary>
[TestFixture]
[Category("Dashboard")]
[Property("TestCase", "DASH-KPI-001")]
[Property("UserStory", "US-DASH-01")]
public class DashKpi001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    private string? _testOrgId;

    private Dictionary<string, string> ImpersonationHeaders(string userId) =>
        new() { [TestConfiguration.ImpersonationHeaderName] = userId };

    [TearDown]
    public async Task CleanupOrg()
    {
        if (_testOrgId == null) return;
        await Page.APIRequest.DeleteAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations/{_testOrgId}",
            new()
            {
                Headers = ImpersonationHeaders(TestUsers.AdminUser.Id),
                IgnoreHTTPSErrors = true
            });
        _testOrgId = null;
    }

    /// <summary>
    /// US-DASH-01: Dashboard page loads for authenticated user with an org
    /// </summary>
    [Test]
    [Description("US-DASH-01: Dashboard page is accessible for authenticated user")]
    public async Task Dashboard_ShouldBeAccessible()
    {
        // ── Arrange: create org ──
        var orgResponse = await Page.APIRequest.PostAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/organizations",
            new()
            {
                Headers = ImpersonationHeaders(TestUsers.AdminUser.Id),
                DataObject = new
                {
                    name = UniqueTestName("DASH Test"),
                    legalName = "Dashboard Test S.r.l.",
                    vatNumber = "IT88880000905",
                    fiscalCode = "88880000905",
                    billingAddress = "Via Dashboard 1",
                    billingCity = "Milano",
                    billingProvince = "MI",
                    billingZipCode = "20100",
                    billingCountryCode = "IT",
                    billingEmail = "dash@test.local"
                },
                IgnoreHTTPSErrors = true
            });
        Assert.That(orgResponse.Status, Is.EqualTo(201));
        var orgJson = await orgResponse.JsonAsync();
        _testOrgId = orgJson?.GetProperty("id").GetString();

        // ── Act: navigate to dashboard ──
        await NavigateToAsync("/dashboard");

        // ── Assert: page loaded ──
        Assert.That(Page.Url, Does.Contain("/dashboard"), "URL should contain /dashboard");
    }
}
