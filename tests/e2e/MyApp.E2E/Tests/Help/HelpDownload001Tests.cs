using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-DOWNLOAD-001: An Admin downloads a question the manual could not answer
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-DOWNLOAD-001")]
[Property("UserStory", "US-HELP-07")]
public class HelpDownload001Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A question the manual cannot answer is in the file the Admin downloads for a period that includes it.")]
    public async Task AnAdminDownloadsAQuestionTheManualCouldNotAnswer()
    {
        var question = UniqueQuestion("can i get an email when someone assigns me a task");
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);
        // A second session is signed in as Admin of "MyApp Dev Org".
        var admin = await StartSecondSession(TestUsers.AdminUser);
        var adminQuestions = new UnansweredQuestionsPage(admin);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Help" button.
        await help.ClickHelp();
        // 3. Click the "Ask" tab.
        await help.ClickAskTab();
        // 4. Type "can i get an email when someone assigns me a task" in the "Your question" field.
        await help.TypeQuestion(question);
        // 5. Click the "Ask" button.
        await help.ClickAsk();
        // 6. As Admin: Open the Unanswered questions page.
        await adminQuestions.Open();
        // 7. As Admin: Type "2020-01-01" in the "From" field.
        await adminQuestions.TypeFrom("2020-01-01");
        // 8. As Admin: Type "2099-12-31" in the "To" field.
        await adminQuestions.TypeTo("2099-12-31");
        // 9. As Admin: Click the "Download" button.
        await adminQuestions.ClickDownload();
        var file = await adminQuestions.DownloadedFile();

        // As Admin: the downloaded file has an entry with the question "can i get an email when someone assigns me a task".
        var entry = file.EntryFor(question);
        Assert.That(entry, Is.Not.Null, "The downloaded file has no entry with the question.");
        // As Admin: the entry shows the language "en" and the reason "no_match".
        Assert.That(entry!.Language, Is.EqualTo("en"));
        Assert.That(entry.Reason, Is.EqualTo("no_match"));
        // As Admin: the entry shows the date and time and the screen where the question was asked.
        Assert.That(entry.RecordedAt, Is.Not.Empty);
        Assert.That(entry.Screen, Is.EqualTo("/tasks"));
    }
}
