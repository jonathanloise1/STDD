using System.Text.RegularExpressions;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-SEARCH-002: A search finds other forms of the same word
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-SEARCH-002")]
[Property("UserStory", "US-HELP-02")]
public class HelpSearch002Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A search for the plural finds sections that use the singular, and the matching words are bold.")]
    public async Task ASearchFindsOtherFormsOfTheSameWord()
    {
        var manual = new ManualPage(Page);

        // 1. Open the Manual page.
        await manual.Open();
        // 2. Type "tasks" in the "Search the manual" field.
        await manual.TypeSearch("tasks");
        // 3. Click the "Search" button.
        await manual.ClickSearch();

        // A result shows the page title "Create a task".
        await Expect(manual.ResultOfPage("Create a task")).ToBeVisibleAsync();
        // In the results, the word "task" is shown in bold.
        await Expect(manual.BoldWords.First).ToHaveTextAsync(new Regex("^[Tt]asks?$"));
    }
}
