using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-NAV-001: A member with an organization lands on the Dashboard page
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-NAV-001")]
[Property("UserStory", "US-AUTH-06")]
public class AuthNav001Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("After signing in, an Admin of an organization lands on the Dashboard page and finds the full menu.")]
    public async Task AMemberWithAnOrganizationLandsOnTheDashboardPage()
    {
        var visitor = new VisitorPage(Page);
        var menu = new SidebarPage(Page);
        var header = new HeaderPage(Page);

        // 1. Open the home page.
        await visitor.OpenHomePage();
        // 2. Sign in as Admin.
        await visitor.SignInAs(TestUsers.AdminUser);
        // The Dashboard page opens.
        await Expect(menu.Entry("Dashboard")).ToHaveAttributeAsync("aria-current", "page");
        // The menu lists "Dashboard", "Tasks", "Team", "Audit Log" and "Settings".
        await Expect(menu.Entry("Dashboard")).ToBeVisibleAsync();
        await Expect(menu.Entry("Tasks")).ToBeVisibleAsync();
        await Expect(menu.Entry("Team")).ToBeVisibleAsync();
        await Expect(menu.Entry("Audit Log")).ToBeVisibleAsync();
        await Expect(menu.Entry("Settings")).ToBeVisibleAsync();
        // The header shows "MyApp Dev Org".
        await Expect(header.OrganizationName(TestUsers.OrgDevName)).ToBeVisibleAsync();
    }
}
