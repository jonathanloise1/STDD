using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-OPEN-008: The panel shows a message when the help cannot be loaded
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-OPEN-008")]
[Property("UserStory", "US-HELP-03")]
public class HelpOpen008Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("When the help content cannot be loaded, the panel says so and offers the manual.")]
    public async Task ThePanelShowsAMessageWhenTheHelpCannotBeLoaded()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);
        // The help content cannot be loaded.
        await TheHelpContentCannotBeLoaded();

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Help" button.
        await help.ClickHelp();

        // The message "Help is unavailable." appears in the panel.
        await Expect(help.PanelMessage("Help is unavailable.")).ToBeVisibleAsync();
        // The panel shows an "Open the manual" link.
        await Expect(help.OpenTheManualLink).ToBeVisibleAsync();
        // The Tasks page is still usable.
        await Expect(tasks.NewTaskButton).ToBeEnabledAsync();
    }
}
