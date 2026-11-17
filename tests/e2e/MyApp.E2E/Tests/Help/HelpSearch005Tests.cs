using NUnit.Framework;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-SEARCH-005: A visitor cannot search the manual
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-SEARCH-005")]
[Property("UserStory", "US-HELP-02")]
public class HelpSearch005Tests : HelpSignedOutTestBase
{
    [Test]
    [Description("A person who is not signed in cannot reach the search results.")]
    public async Task AVisitorCannotSearchTheManual()
    {
        var manual = new ManualPage(Page);
        var signIn = new SignInPage(Page);

        // 1. Open the Manual page.
        await manual.Open();

        // The Sign in page opens.
        await signIn.ExpectOpen();
        // The "Search the manual" field is not shown.
        await Expect(manual.SearchField).ToHaveCountAsync(0);
    }
}
