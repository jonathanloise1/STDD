using System.Text.RegularExpressions;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ASK-001: A question shows the matching sections of the manual
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ASK-001")]
[Property("UserStory", "US-HELP-04")]
public class HelpAsk001Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A question typed in the Ask tab shows cards with the best matching sections at once.")]
    public async Task AQuestionShowsTheMatchingSectionsOfTheManual()
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

        // The header "Best matches in the manual" appears.
        await Expect(help.BestMatchesHeader).ToBeVisibleAsync();
        // At most three cards are shown.
        Assert.That(await help.CountCards(), Is.InRange(1, 3));
        // The first card shows the page title "Create a task", a section heading and a short text.
        await Expect(help.FirstCardPageTitle).ToHaveTextAsync("Create a task");
        await Expect(help.FirstCardHeading).ToHaveTextAsync(new Regex(".+"));
        await Expect(help.FirstCardText).ToHaveTextAsync(new Regex(".+"));
        // Each card has an "Open in the manual" link.
        await Expect(help.CardsWithoutLink).ToHaveCountAsync(0);
        // No card is labelled "Answer from the manual".
        await Expect(help.AnswerLabel).ToHaveCountAsync(0);
    }
}
