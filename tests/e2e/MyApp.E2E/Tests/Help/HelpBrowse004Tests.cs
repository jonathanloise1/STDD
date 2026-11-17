using NUnit.Framework;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-BROWSE-004: A visitor cannot read the manual
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-BROWSE-004")]
[Property("UserStory", "US-HELP-01")]
public class HelpBrowse004Tests : HelpSignedOutTestBase
{
    [Test]
    [Description("A person who is not signed in is asked to sign in instead of seeing the manual.")]
    public async Task AVisitorCannotReadTheManual()
    {
        var manual = new ManualPage(Page);
        var signIn = new SignInPage(Page);

        // 1. Open the Manual page.
        await manual.Open();

        // The Sign in page opens.
        await signIn.ExpectOpen();
        // The list of areas is not shown.
        await Expect(manual.ListedLinks).ToHaveCountAsync(0);
    }
}
