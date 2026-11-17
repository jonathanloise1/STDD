using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ANSWER-004: A declined answer shows the closest sections
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ANSWER-004")]
[Property("UserStory", "US-HELP-05")]
public class HelpAnswer004Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("When the assistant declines to answer, the panel shows a fixed message and the closest sections.")]
    public async Task ADeclinedAnswerShowsTheClosestSections()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);
        // The assistant is available on the server and declines to answer "How do I create a task".
        await ServerAssistantDeclines();

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

        // The message "The assistant could not answer this from the manual. These sections are the closest:" appears.
        await Expect(help.Text("The assistant could not answer this from the manual. These sections are the closest:")).ToBeVisibleAsync();
        // The section cards are shown below the message.
        await Expect(help.SectionCards.First).ToBeVisibleAsync();
        await Expect(help.CardsAboveTheDeclinedMessage).ToHaveCountAsync(0);
        // The sentence the assistant used to decline is not shown.
        await Expect(help.Text(DeclineSentence)).ToHaveCountAsync(0);
    }
}
