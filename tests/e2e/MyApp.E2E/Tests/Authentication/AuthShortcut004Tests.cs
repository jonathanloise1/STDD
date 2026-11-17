using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-SHORTCUT-004: The question mark opens the shortcuts help
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-SHORTCUT-004")]
[Property("UserStory", "US-AUTH-09")]
public class AuthShortcut004Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Pressing the question mark opens a dialog that explains the shortcuts; an Admin sees all five pages.")]
    public async Task TheQuestionMarkOpensTheShortcutsHelp()
    {
        var address = new AddressPage(Page);
        var shortcuts = new ShortcutsPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // 2. Press "?".
        await shortcuts.PressQuestionMark();
        // The "Keyboard Shortcuts" dialog opens with the text "Press G then a letter to navigate to a section."
        await Expect(shortcuts.Dialog).ToBeVisibleAsync();
        await Expect(shortcuts.Hint).ToBeVisibleAsync();
        // The dialog lists "Dashboard", "Tasks", "Audit Log", "Team" and "Settings".
        await Expect(shortcuts.RowFor("Dashboard")).ToHaveCountAsync(1);
        await Expect(shortcuts.RowFor("Tasks")).ToHaveCountAsync(1);
        await Expect(shortcuts.RowFor("Audit Log")).ToHaveCountAsync(1);
        await Expect(shortcuts.RowFor("Team")).ToHaveCountAsync(1);
        await Expect(shortcuts.RowFor("Settings")).ToHaveCountAsync(1);
    }
}
