using NUnit.Framework;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-OPEN-007: A visitor does not get the Help button
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-OPEN-007")]
[Property("UserStory", "US-HELP-03")]
public class HelpOpen007Tests : HelpSignedOutTestBase
{
    [Test]
    [Description("A person who is not signed in is asked to sign in and sees no help.")]
    public async Task AVisitorDoesNotGetTheHelpButton()
    {
        var tasks = ScreenPage.Tasks(Page);
        var help = new HelpPanelPage(Page);
        var signIn = new SignInPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();

        // The Sign in page opens.
        await signIn.ExpectOpen();
        // The page shows no "Help" button.
        await Expect(help.HelpButton).ToHaveCountAsync(0);
    }
}
