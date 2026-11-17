using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-SEARCH-004: A search with no match shows a message and a link
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-SEARCH-004")]
[Property("UserStory", "US-HELP-02")]
public class HelpSearch004Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("When no section matches, the page says so and offers a way back to the manual.")]
    public async Task ASearchWithNoMatchShowsAMessageAndALink()
    {
        var manual = new ManualPage(Page);

        // 1. Open the Manual page.
        await manual.Open();
        // 2. Type "zzqxj" in the "Search the manual" field.
        await manual.TypeSearch("zzqxj");
        // 3. Click the "Search" button.
        await manual.ClickSearch();

        // The message "Nothing found in the manual." appears.
        await Expect(manual.NothingFoundMessage).ToBeVisibleAsync();
        // The page shows a "Go to the manual home" link.
        await Expect(manual.GoToTheManualHomeLink).ToBeVisibleAsync();
        // Clicking the link opens the home page of the manual.
        await manual.ClickGoToTheManualHome();
        await Expect(manual.ListedLink("Tasks")).ToBeVisibleAsync();
    }
}
