using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-OPEN-006: Closing the panel returns to the unchanged screen
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-OPEN-006")]
[Property("UserStory", "US-HELP-03")]
public class HelpOpen006Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("After the panel is closed the user is back on the same screen with what they had typed.")]
    public async Task ClosingThePanelReturnsToTheUnchangedScreen()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type "Draft kept after help" in the "Title" field.
        await tasks.TypeTitle("Draft kept after help");
        // 4. Click the "Help" button.
        await help.ClickHelp();
        // 5. Press the "Escape" key.
        await help.PressEscape();

        // The panel closes.
        await Expect(help.Panel).ToBeHiddenAsync();
        // The "New Task" form is still open and the "Title" field shows "Draft kept after help".
        await Expect(tasks.TitleField).ToHaveValueAsync("Draft kept after help");
    }
}
