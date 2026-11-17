using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-SHORTCUT-001: G then T opens the Tasks page
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-SHORTCUT-001")]
[Property("UserStory", "US-AUTH-09")]
public class AuthShortcut001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An Admin moves between pages with the keyboard: G then T opens the Tasks page and G then M the Team page.")]
    public async Task GThenTOpensTheTasksPage()
    {
        var address = new AddressPage(Page);
        var menu = new SidebarPage(Page);
        var shortcuts = new ShortcutsPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // 2. Press "G" then "T".
        await shortcuts.PressGThen("t");
        // After step 2, the Tasks page opens.
        await Expect(menu.Entry("Tasks")).ToHaveAttributeAsync("aria-current", "page");
        // 3. Press "G" then "M".
        await shortcuts.PressGThen("m");
        // After step 3, the Team page opens.
        await Expect(menu.Entry("Team")).ToHaveAttributeAsync("aria-current", "page");
    }
}
