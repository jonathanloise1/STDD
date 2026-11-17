using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-DOWNLOAD-002: A period ending before it starts shows a field message
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-DOWNLOAD-002")]
[Property("UserStory", "US-HELP-07")]
public class HelpDownload002Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("The Download is refused when the To date is before the From date.")]
    public async Task APeriodEndingBeforeItStartsShowsAFieldMessage()
    {
        var questions = new UnansweredQuestionsPage(Page);

        // 1. Open the Unanswered questions page.
        await questions.Open();
        // 2. Type "2030-02-01" in the "From" field.
        await questions.TypeFrom("2030-02-01");
        // 3. Type "2030-01-01" in the "To" field.
        await questions.TypeTo("2030-01-01");
        // 4. Click the "Download" button.
        await questions.ClickDownload();

        // The "To" field shows "The end date cannot be before the start date.".
        await Expect(questions.ToFieldMessage).ToHaveTextAsync("The end date cannot be before the start date.");
        // No file is downloaded.
        Assert.That(questions.FilesDownloaded, Is.EqualTo(0));
    }
}
