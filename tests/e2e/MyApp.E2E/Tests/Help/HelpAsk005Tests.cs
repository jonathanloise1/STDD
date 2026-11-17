using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ASK-005: A question shows at most three cards
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ASK-005")]
[Property("UserStory", "US-HELP-04")]
public class HelpAsk005Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A broad question shows three cards, never more.")]
    public async Task AQuestionShowsAtMostThreeCards()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Help" button.
        await help.ClickHelp();
        // 3. Click the "Ask" tab.
        await help.ClickAskTab();
        // 4. Type "task" in the "Your question" field.
        await help.TypeQuestion("task");
        // 5. Click the "Ask" button.
        await help.ClickAsk();

        // Exactly three cards are shown.
        await Expect(help.SectionCards).ToHaveCountAsync(3);
    }
}
