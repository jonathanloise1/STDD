using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-SHORTCUT-002: An Editor has only the shortcuts of Dashboard and Tasks
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-SHORTCUT-002")]
[Property("UserStory", "US-AUTH-09")]
public class AuthShortcut002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("For an Editor, the shortcut to the Team page does nothing and the help dialog lists only the pages the role allows.")]
    public async Task AnEditorHasOnlyTheShortcutsOfDashboardAndTasks()
    {
        var address = new AddressPage(Page);
        var menu = new SidebarPage(Page);
        var shortcuts = new ShortcutsPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // 2. Press "G" then "M".
        await shortcuts.PressGThen("m");
        // After step 2, the Dashboard page is still shown and no message appears.
        await Expect(menu.Entry("Dashboard")).ToHaveAttributeAsync("aria-current", "page");
        await Expect(address.Alerts).ToHaveCountAsync(0);
        // 3. Press "?".
        await shortcuts.PressQuestionMark();
        // After step 3, the "Keyboard Shortcuts" dialog lists "Dashboard" and "Tasks" and does not list "Team", "Audit Log" or "Settings".
        await Expect(shortcuts.Dialog).ToBeVisibleAsync();
        await Expect(shortcuts.RowFor("Dashboard")).ToHaveCountAsync(1);
        await Expect(shortcuts.RowFor("Tasks")).ToHaveCountAsync(1);
        await Expect(shortcuts.RowFor("Team")).ToHaveCountAsync(0);
        await Expect(shortcuts.RowFor("Audit Log")).ToHaveCountAsync(0);
        await Expect(shortcuts.RowFor("Settings")).ToHaveCountAsync(0);
    }
}
