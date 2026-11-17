using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-NAV-002: The open page is highlighted in the menu
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-NAV-002")]
[Property("UserStory", "US-AUTH-06")]
public class AuthNav002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Clicking a menu entry opens its page and highlights only that entry.")]
    public async Task TheOpenPageIsHighlightedInTheMenu()
    {
        var address = new AddressPage(Page);
        var menu = new SidebarPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // 2. Click the "Tasks" link in the menu.
        await menu.ClickTasks();
        // The Tasks page opens and the "Tasks" entry is highlighted.
        await Expect(menu.Entry("Tasks")).ToHaveAttributeAsync("aria-current", "page");
        // The "Dashboard" entry is not highlighted.
        await Expect(menu.Entry("Dashboard")).Not.ToHaveAttributeAsync("aria-current", "page");
    }
}
