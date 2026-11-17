using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-ACCESS-002: An Editor sees no management pages
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-ACCESS-002")]
[Property("UserStory", "US-AUTH-07")]
public class AuthAccess002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("An Editor's menus offer no management page, and opening the Team page by its address leads back to the Dashboard with a message.")]
    public async Task AnEditorSeesNoManagementPages()
    {
        var address = new AddressPage(Page);
        var menu = new SidebarPage(Page);
        var menus = new HeaderMenusPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // The menu shows only "Dashboard" and "Tasks" (no management page).
        await Expect(menu.Entry("Dashboard")).ToBeVisibleAsync();
        await Expect(menu.Entry("Tasks")).ToBeVisibleAsync();
        await Expect(menu.Entry("Team")).ToHaveCountAsync(0);
        await Expect(menu.Entry("Audit Log")).ToHaveCountAsync(0);
        await Expect(menu.Entry("Settings")).ToHaveCountAsync(0);
        // 2. Click the "Dev Member" button.
        await menus.ClickAvatar("Dev Member");
        // After step 2, the avatar menu shows "Logout" and no "Settings".
        await Expect(menus.LogoutInMenu).ToBeVisibleAsync();
        await Expect(menus.SettingsInMenu).ToHaveCountAsync(0);
        // 3. Open the Team page.
        await address.OpenTeamPage();
        // After step 3, the Dashboard page opens with the message "You don't have access to this page."
        await Expect(menu.Entry("Dashboard")).ToHaveAttributeAsync("aria-current", "page");
        await Expect(address.Message("You don't have access to this page.")).ToBeVisibleAsync();
        // 4. Click the "Tasks" link in the menu.
        await menu.ClickTasks();
        // After step 4, the Tasks page opens.
        await Expect(menu.Entry("Tasks")).ToHaveAttributeAsync("aria-current", "page");
    }
}
