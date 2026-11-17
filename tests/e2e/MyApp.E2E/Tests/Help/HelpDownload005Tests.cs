using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-DOWNLOAD-005: A period with no questions gives a file with no entries
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-DOWNLOAD-005")]
[Property("UserStory", "US-HELP-07")]
public class HelpDownload005Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An Admin who downloads a period without unanswered questions gets a file with nothing in it.")]
    public async Task APeriodWithNoQuestionsGivesAFileWithNoEntries()
    {
        var questions = new UnansweredQuestionsPage(Page);

        // 1. Open the Unanswered questions page.
        await questions.Open();
        // 2. Type "2001-01-01" in the "From" field.
        await questions.TypeFrom("2001-01-01");
        // 3. Type "2001-01-31" in the "To" field.
        await questions.TypeTo("2001-01-31");
        // 4. Click the "Download" button.
        await questions.ClickDownload();

        // A file is downloaded.
        var file = await questions.DownloadedFile();
        // The file has no entries.
        Assert.That(file.Entries, Is.Empty);
    }
}
