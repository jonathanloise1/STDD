using System.Text.RegularExpressions;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-SEARCH-001: Searching shows the matching sections of the manual
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-SEARCH-001")]
[Property("UserStory", "US-HELP-02")]
public class HelpSearch001Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A search for two words lists the sections that match, each with its page, heading and a short text.")]
    public async Task SearchingShowsTheMatchingSectionsOfTheManual()
    {
        var manual = new ManualPage(Page);

        // 1. Open the Manual page.
        await manual.Open();
        // 2. Type "create task" in the "Search the manual" field.
        await manual.TypeSearch("create task");
        // 3. Click the "Search" button.
        await manual.ClickSearch();

        // The results include a result with the page title "Create a task".
        await Expect(manual.ResultOfPage("Create a task")).ToBeVisibleAsync();
        // Each result shows a page title, a section heading and a short text of at most 160 characters.
        await Expect(manual.ResultTitle(manual.Results.First)).ToHaveTextAsync(new Regex(".+"));
        await Expect(manual.ResultHeading(manual.Results.First)).ToHaveTextAsync(new Regex(".+"));
        await Expect(manual.ResultSnippet(manual.Results.First)).ToHaveTextAsync(new Regex(".+"));
        Assert.That(await manual.SnippetLengths(), Is.All.LessThanOrEqualTo(160));
        // Clicking the result opens the page "Create a task" in the manual.
        await manual.ClickResult("Create a task");
        await Expect(manual.PageTitle("Create a task")).ToBeVisibleAsync();
    }
}
