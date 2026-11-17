using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-OPEN-002: Clicking a page in the panel opens it in the manual
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-OPEN-002")]
[Property("UserStory", "US-HELP-03")]
public class HelpOpen002Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A page chosen in the panel opens in the manual.")]
    public async Task ClickingAPageInThePanelOpensItInTheManual()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);
        var manual = new ManualPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Help" button.
        await help.ClickHelp();
        // 3. Click the "Create a task" link.
        await help.ClickPage("Create a task");

        // The Manual page shows the page "Create a task".
        await Expect(manual.PageTitle("Create a task")).ToBeVisibleAsync();
        // The section "Steps" is shown on the page.
        await Expect(manual.SectionHeading("Steps")).ToBeVisibleAsync();
    }
}
