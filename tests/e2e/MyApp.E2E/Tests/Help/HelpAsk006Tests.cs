using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ASK-006: The fixed reply links to a search for the same words
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ASK-006")]
[Property("UserStory", "US-HELP-04")]
public class HelpAsk006Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("The search link of the fixed reply opens the search results for the question.")]
    public async Task TheFixedReplyLinksToASearchForTheSameWords()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);
        var manual = new ManualPage(Page);

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
        // 6. Click the "Search the manual for this" link.
        await help.ClickSearchTheManualForThis();

        // The Manual page shows the search results.
        await Expect(manual.SearchOutcome).ToBeVisibleAsync();
        // The "Search the manual" field shows "Can I pay my invoice from here?".
        await Expect(manual.SearchField).ToHaveValueAsync("Can I pay my invoice from here?");
    }
}
