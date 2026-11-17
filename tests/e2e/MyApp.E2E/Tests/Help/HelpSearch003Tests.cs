using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-SEARCH-003: The results show at most ten sections
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-SEARCH-003")]
[Property("UserStory", "US-HELP-02")]
public class HelpSearch003Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A search for a word that many sections contain still lists no more than ten.")]
    public async Task TheResultsShowAtMostTenSections()
    {
        var manual = new ManualPage(Page);

        // 1. Open the Manual page.
        await manual.Open();
        // 2. Type "task" in the "Search the manual" field.
        await manual.TypeSearch("task");
        // 3. Click the "Search" button.
        await manual.ClickSearch();

        // The results list shows exactly ten sections.
        await Expect(manual.Results).ToHaveCountAsync(10);
        // The best matching page is first in the list.
        await Expect(manual.Results.First).ToBeVisibleAsync();
    }
}
