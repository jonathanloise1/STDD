using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ANSWER-003: An answer without a source is never shown
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ANSWER-003")]
[Property("UserStory", "US-HELP-05")]
public class HelpAnswer003Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("When the assistant gives an answer with no source, the panel shows the closest sections instead.")]
    public async Task AnAnswerWithoutASourceIsNeverShown()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);
        // The assistant is available on the server and answers "How do I create a task" with no source.
        await ServerAssistantAnswersWithNoSource();

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
        // No card is labelled "Answer from the manual".
        await Expect(help.AnswerLabel).ToHaveCountAsync(0);
        // The section cards are shown.
        await Expect(help.SectionCards.First).ToBeVisibleAsync();
    }
}
