using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ASK-004: The matches come from screens other than the current one
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ASK-004")]
[Property("UserStory", "US-HELP-04")]
public class HelpAsk004Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("On the Dashboard page, a question about deleting a task shows the page about deleting a task.")]
    public async Task TheMatchesComeFromScreensOtherThanTheCurrentOne()
    {
        var dashboard = new DashboardPage(Page);
        var help = new HelpPanelPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();
        // 2. Click the "Help" button.
        await help.ClickHelp();
        // 3. Click the "Ask" tab.
        await help.ClickAskTab();
        // 4. Type "How do I delete a task" in the "Your question" field.
        await help.TypeQuestion("How do I delete a task");
        // 5. Click the "Ask" button.
        await help.ClickAsk();

        // A card shows the page title "Delete a task".
        await Expect(help.CardOfPage("Delete a task")).ToBeVisibleAsync();
    }
}
