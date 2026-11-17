using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-OPEN-003: The page about a form comes first in the panel
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-OPEN-003")]
[Property("UserStory", "US-HELP-03")]
public class HelpOpen003Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("The Help button of a form lists the page about that form before the other pages of the screen.")]
    public async Task ThePageAboutAFormComesFirstInThePanel()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Click the "Help" button of the "New Task" form.
        await help.ClickHelpOfNewTaskForm();

        // The first entry of the list is "Create a task".
        await Expect(help.PageLinks.First).ToHaveTextAsync("Create a task");
        // The list also shows "See the list of tasks" after it.
        await Expect(help.PageLink("See the list of tasks")).ToBeVisibleAsync();
    }
}
