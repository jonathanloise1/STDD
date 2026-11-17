using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ASK-008: Asking shows a message when the help cannot be loaded
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ASK-008")]
[Property("UserStory", "US-HELP-04")]
public class HelpAsk008Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("When the help content cannot be loaded, asking a question says help is unavailable.")]
    public async Task AskingShowsAMessageWhenTheHelpCannotBeLoaded()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);
        // The help content cannot be loaded.
        await TheHelpContentCannotBeLoaded();

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Help" button.
        await help.ClickHelp();
        // 3. Click the "Ask" tab.
        await help.ClickAskTab();
        // 4. Type "How do I create a task" in the "Your question" field.
        await help.TypeQuestion("How do I create a task");
        // 5. Click the "Ask" button.
        await help.ClickAsk();

        // The message "Help is unavailable." appears.
        await Expect(help.PanelMessage("Help is unavailable.")).ToBeVisibleAsync();
        // No card is shown.
        await Expect(help.SectionCards).ToHaveCountAsync(0);
    }
}
