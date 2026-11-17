using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-OPEN-005: Every screen has a Help button in its header
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-OPEN-005")]
[Property("UserStory", "US-HELP-03")]
public class HelpOpen005Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("The Help button is in the header of different screens.")]
    public async Task EveryScreenHasAHelpButtonInItsHeader()
    {
        var dashboard = new DashboardPage(Page);
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();
        // The header of the Dashboard page shows a "Help" button.
        await Expect(help.HelpButton).ToBeVisibleAsync();
        // 2. Open the Tasks page.
        await tasks.Open();
        // The header of the Tasks page shows a "Help" button.
        await Expect(help.HelpButton).ToBeVisibleAsync();
    }
}
