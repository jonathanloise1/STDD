using NUnit.Framework;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ASK-007: A visitor cannot ask a question
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ASK-007")]
[Property("UserStory", "US-HELP-04")]
public class HelpAsk007Tests : HelpSignedOutTestBase
{
    [Test]
    [Description("A person who is not signed in cannot reach the Ask tab.")]
    public async Task AVisitorCannotAskAQuestion()
    {
        var tasks = ScreenPage.Tasks(Page);
        var help = new HelpPanelPage(Page);
        var signIn = new SignInPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();

        // The Sign in page opens.
        await signIn.ExpectOpen();
        // The page shows no "Ask" tab and no "Your question" field.
        await Expect(help.AskTab).ToHaveCountAsync(0);
        await Expect(help.QuestionField).ToHaveCountAsync(0);
    }
}
