using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-SEARCH-006: A search shows a message when the manual cannot be loaded
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-SEARCH-006")]
[Property("UserStory", "US-HELP-02")]
public class HelpSearch006Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("When the content needed for the search cannot be loaded, the search says help is unavailable.")]
    public async Task ASearchShowsAMessageWhenTheManualCannotBeLoaded()
    {
        var manual = new ManualPage(Page);
        // The content of the manual cannot be loaded for the search.
        await TheContentForTheSearchCannotBeLoaded();

        // 1. Open the Manual page.
        await manual.Open();
        // 2. Type "create task" in the "Search the manual" field.
        await manual.TypeSearch("create task");
        // 3. Click the "Search" button.
        await manual.ClickSearch();

        // The message "Help is unavailable." appears.
        await Expect(manual.UnavailableMessage).ToBeVisibleAsync();
        // The results list is not shown.
        await Expect(manual.Results).ToHaveCountAsync(0);
    }
}
