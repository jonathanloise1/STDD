using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Manual.Organizations;

/// <summary>Screenshots of the Organization page for the manual (manual.md 4).</summary>
[TestFixture]
[Category("organizations")]
[Category("manual-capture")]
[Property("UserStory", "US-ORG-01")]
public class OrganizationCapture : ManualCaptureBase
{
    protected override TestUser GetTestUser(string state) => TestUsers.AdminUser;

    [Test]
    public async Task Default()
    {
        var organization = new OrganizationPage(Page);
        await organization.Open();
        await Expect(organization.Title).ToBeVisibleAsync();
        await ShootAsync(organization);
    }

    [Test]
    public async Task Unavailable()
    {
        var organization = new OrganizationPage(Page);
        ServiceDoesNotAnswer("ORG-VIEW-003");
        await organization.Open();
        await WaitForMessageAsync();
        await ShootAsync(organization);
    }
}
