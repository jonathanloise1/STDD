using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-SHORTCUT-003: Typing in a field does not trigger shortcuts
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-SHORTCUT-003")]
[Property("UserStory", "US-AUTH-09")]
public class AuthShortcut003Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("Letters typed in a field are text: typing G and D in the Title field keeps the user on the Tasks page.")]
    public async Task TypingInAFieldDoesNotTriggerShortcuts()
    {
        var tasks = new TasksPage(Page);
        var menu = new SidebarPage(Page);
        var shortcuts = new ShortcutsPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type "gd" in the "Title" field.
        await shortcuts.TypeInTitleField("gd");
        // The Tasks page is still shown with the form open.
        await Expect(menu.Entry("Tasks")).ToHaveAttributeAsync("aria-current", "page");
        await Expect(tasks.TitleField).ToBeVisibleAsync();
        // The "Title" field shows "gd".
        await Expect(tasks.FormField("Title")).ToHaveValueAsync("gd");
    }
}
