using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ASK-003: A question outside the application gets a fixed reply
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ASK-003")]
[Property("UserStory", "US-HELP-04")]
public class HelpAsk003Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A question about something the application does not do shows the fixed reply and two links.")]
    public async Task AQuestionOutsideTheApplicationGetsAFixedReply()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Help" button.
        await help.ClickHelp();
        // 3. Click the "Ask" tab.
        await help.ClickAskTab();
        // 4. Type "Can I pay my invoice from here?" in the "Your question" field.
        await help.TypeQuestion("Can I pay my invoice from here?");
        // 5. Click the "Ask" button.
        await help.ClickAsk();

        // The message "The manual has nothing on this yet." appears.
        await Expect(help.Text("The manual has nothing on this yet.")).ToBeVisibleAsync();
        // The panel shows the links "Go to the manual home" and "Search the manual for this".
        await Expect(help.AskLink("Go to the manual home")).ToBeVisibleAsync();
        await Expect(help.AskLink("Search the manual for this")).ToBeVisibleAsync();
        // No answer card is shown.
        await Expect(help.AnswerLabel).ToHaveCountAsync(0);
    }
}
