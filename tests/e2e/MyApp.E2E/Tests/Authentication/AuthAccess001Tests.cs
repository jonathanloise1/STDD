using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-ACCESS-001: An Admin opens the management pages from the menu
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-ACCESS-001")]
[Property("UserStory", "US-AUTH-07")]
public class AuthAccess001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An Admin finds the management pages in the menu and can open each of them.")]
    public async Task AnAdminOpensTheManagementPagesFromTheMenu()
    {
        var address = new AddressPage(Page);
        var menu = new SidebarPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // The menu shows "Dashboard", "Tasks", "Team", "Audit Log" and "Settings".
        await Expect(menu.Entry("Dashboard")).ToBeVisibleAsync();
        await Expect(menu.Entry("Tasks")).ToBeVisibleAsync();
        await Expect(menu.Entry("Team")).ToBeVisibleAsync();
        await Expect(menu.Entry("Audit Log")).ToBeVisibleAsync();
        await Expect(menu.Entry("Settings")).ToBeVisibleAsync();
        // 2. Click the "Settings" link in the menu.
        await menu.ClickSettings();
        // After step 2, the Organization page opens.
        await Expect(menu.Entry("Settings")).ToHaveAttributeAsync("aria-current", "page");
        // 3. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // After step 3, the Team page opens.
        await Expect(menu.Entry("Team")).ToHaveAttributeAsync("aria-current", "page");
        // 4. Click the "Audit Log" link in the menu.
        await menu.ClickAuditLog();
        // After step 4, the Audit Log page opens.
        await Expect(menu.Entry("Audit Log")).ToHaveAttributeAsync("aria-current", "page");
    }
}
