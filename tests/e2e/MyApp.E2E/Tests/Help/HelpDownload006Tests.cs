using NUnit.Framework;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-DOWNLOAD-006: A visitor cannot open the unanswered questions
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-DOWNLOAD-006")]
[Property("UserStory", "US-HELP-07")]
public class HelpDownload006Tests : HelpSignedOutTestBase
{
    [Test]
    [Description("A person who is not signed in is asked to sign in.")]
    public async Task AVisitorCannotOpenTheUnansweredQuestions()
    {
        var questions = new UnansweredQuestionsPage(Page);
        var signIn = new SignInPage(Page);

        // 1. Open the Unanswered questions page.
        await questions.Open();

        // The Sign in page opens.
        await signIn.ExpectOpen();
        // The "Download" button is not shown.
        await Expect(questions.DownloadButton).ToHaveCountAsync(0);
    }
}
