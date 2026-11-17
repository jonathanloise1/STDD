using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-DOWNLOAD-009: A failed download shows a message
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-DOWNLOAD-009")]
[Property("UserStory", "US-HELP-07")]
public class HelpDownload009Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("When the file cannot be prepared, the Admin is told and no file is downloaded.")]
    public async Task AFailedDownloadShowsAMessage()
    {
        var questions = new UnansweredQuestionsPage(Page);
        // The file with the questions cannot be prepared.
        ServiceDoesNotAnswer();

        // 1. Open the Unanswered questions page.
        await questions.Open();
        // 2. Type "2020-01-01" in the "From" field.
        await questions.TypeFrom("2020-01-01");
        // 3. Type "2099-12-31" in the "To" field.
        await questions.TypeTo("2099-12-31");
        // 4. Click the "Download" button.
        await questions.ClickDownload();

        // The message "The questions could not be downloaded." appears.
        await Expect(questions.Message("The questions could not be downloaded.")).ToBeVisibleAsync();
        // No file is downloaded.
        Assert.That(questions.FilesDownloaded, Is.EqualTo(0));
    }
}
