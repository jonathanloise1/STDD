using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-RATE-001: Saying yes shows a thank-you message
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-RATE-001")]
[Property("UserStory", "US-HELP-06")]
public class HelpRate001Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("After a result the panel asks whether it helped, and a click on Yes is acknowledged.")]
    public async Task SayingYesShowsAThankYouMessage()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);

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

        // Under the cards the panel asks "Did this help?" with the buttons "Yes" and "No".
        await Expect(help.Text("Did this help?")).ToBeVisibleAsync();
        await Expect(help.AskButtonNamed("Yes")).ToBeVisibleAsync();
        await Expect(help.AskButtonNamed("No")).ToBeVisibleAsync();
        // 6. Click the "Yes" button.
        await help.ClickYes();

        // The message "Thanks for your feedback." appears.
        await Expect(help.Text("Thanks for your feedback.")).ToBeVisibleAsync();
        // The cards are still shown.
        await Expect(help.SectionCards.First).ToBeVisibleAsync();
    }
}
