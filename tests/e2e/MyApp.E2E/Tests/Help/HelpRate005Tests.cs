using NUnit.Framework;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-RATE-005: A visitor cannot rate an answer
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-RATE-005")]
[Property("UserStory", "US-HELP-06")]
public class HelpRate005Tests : HelpSignedOutTestBase
{
    [Test]
    [Description("A person who is not signed in cannot reach the question Did this help.")]
    public async Task AVisitorCannotRateAnAnswer()
    {
        var tasks = ScreenPage.Tasks(Page);
        var help = new HelpPanelPage(Page);
        var signIn = new SignInPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();

        // The Sign in page opens.
        await signIn.ExpectOpen();
        // The page does not show "Did this help?".
        await Expect(help.Text("Did this help?")).ToHaveCountAsync(0);
    }
}
