using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-ACCESS-003: A Viewer sees no management pages
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-ACCESS-003")]
[Property("UserStory", "US-AUTH-07")]
public class AuthAccess003Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.ViewerUser;

    [Test]
    [Description("A Viewer's menus offer no management page, and opening the Organization page by its address leads back to the Dashboard with a message.")]
    public async Task AViewerSeesNoManagementPages()
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
        // 2. Click the "Dev Viewer" button.
        await menus.ClickAvatar("Dev Viewer");
        // After step 2, the avatar menu shows "Logout" and no "Settings".
        await Expect(menus.LogoutInMenu).ToBeVisibleAsync();
        await Expect(menus.SettingsInMenu).ToHaveCountAsync(0);
        // 3. Open the Organization page.
        await address.OpenOrganizationPage();
        // After step 3, the Dashboard page opens with the message "You don't have access to this page."
        await Expect(menu.Entry("Dashboard")).ToHaveAttributeAsync("aria-current", "page");
        await Expect(address.Message("You don't have access to this page.")).ToBeVisibleAsync();
        // 4. Click the "Tasks" link in the menu.
        await menu.ClickTasks();
        // After step 4, the Tasks page opens.
        await Expect(menu.Entry("Tasks")).ToHaveAttributeAsync("aria-current", "page");
    }
}
