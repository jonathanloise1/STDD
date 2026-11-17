using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ANSWER-005: A slow answer shows the best matches instead
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ANSWER-005")]
[Property("UserStory", "US-HELP-05")]
public class HelpAnswer005Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("When the only assistant takes too long, the panel shows the best matching sections.")]
    public async Task ASlowAnswerShowsTheBestMatchesInstead()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);
        // The assistant is available on the server and takes too long to answer "How do I create a task".
        await ServerAssistantTakesTooLong();

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

        // The header "Best matches in the manual" appears above the section cards.
        await Expect(help.BestMatchesHeader).ToBeVisibleAsync();
        await Expect(help.SectionCards.First).ToBeVisibleAsync();
        // No card is labelled "Answer from the manual".
        await Expect(help.AnswerLabel).ToHaveCountAsync(0);
    }
}
