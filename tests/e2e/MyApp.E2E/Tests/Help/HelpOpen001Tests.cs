using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-OPEN-001: The Help button opens the pages of the current screen
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-OPEN-001")]
[Property("UserStory", "US-HELP-03")]
public class HelpOpen001Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("On the Tasks page, the Help button opens a panel that lists the manual pages of that screen.")]
    public async Task TheHelpButtonOpensThePagesOfTheCurrentScreen()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Help" button.
        await help.ClickHelp();

        // A side panel opens next to the Tasks page with the tabs "This page" and "Ask".
        await Expect(help.Panel).ToBeVisibleAsync();
        await Expect(help.ThisPageTab).ToBeVisibleAsync();
        await Expect(help.AskTab).ToBeVisibleAsync();
        // The tab "This page" is open.
        await Expect(help.ThisPageTab).ToHaveAttributeAsync("aria-selected", "true");
        // The list shows the page "Create a task".
        await Expect(help.PageLink("Create a task")).ToBeVisibleAsync();
        // The Tasks page is still shown behind the panel.
        await Expect(tasks.Table).ToBeVisibleAsync();
    }
}
