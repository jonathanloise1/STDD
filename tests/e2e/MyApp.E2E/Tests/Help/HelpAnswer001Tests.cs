using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ANSWER-001: An answer shows its source and where it was written
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ANSWER-001")]
[Property("UserStory", "US-HELP-05")]
public class HelpAnswer001Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A short answer appears above the section cards, with a source link and a footer line.")]
    public async Task AnAnswerShowsItsSourceAndWhereItWasWritten()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);
        // The assistant is available on the server and answers "How do I create a task" citing a section of the page "Create a task" as its source.
        await ServerAssistantAnswersCitingCreateATask();

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

        // A card labelled "Answer from the manual" appears above the section cards.
        await Expect(help.AnswerLabel).ToBeVisibleAsync();
        await Expect(help.FirstCardPageTitle).ToBeVisibleAsync();
        await Expect(help.CardsAboveTheAnswer).ToHaveCountAsync(0);
        // The answer shows one source link.
        await Expect(help.SourceLinks).ToHaveCountAsync(1);
        // The footer line reads "Answered by the server".
        await Expect(help.AnswerFooter("Answered by the server")).ToBeVisibleAsync();
        // The first section card is the cited one, from the page "Create a task".
        await Expect(help.FirstCardPageTitle).ToHaveTextAsync("Create a task");
    }
}
