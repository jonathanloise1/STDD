using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-VIEW-002: A viewer cannot open the Organization page
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-VIEW-002")]
[Property("UserStory", "US-ORG-01")]
public class OrgView002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.ViewerUser;

    [Test]
    [Description("A viewer has no \"Settings\" menu item, and opening the Organization page by its address leads to the Dashboard with a message.")]
    public async Task OpeningTheOrganizationPageLeadsToTheDashboard()
    {
        var menu = new SidebarPage(Page);
        var org = new OrganizationPage(Page);
        var noAccess = new NoAccessPage(Page);

        // 1. Open the Organization page.
        await org.Open();

        // The Dashboard page opens with the message "You don't have access to this page."
        await noAccess.ExpectDashboardOpen();
        await Expect(noAccess.NoAccessMessage).ToBeVisibleAsync();
        // The page does not show "Billing Information".
        await Expect(org.Title).ToHaveCountAsync(0);
        // The menu has no "Settings" link.
        await Expect(menu.Entry("Settings")).ToHaveCountAsync(0);
    }
}
