using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-RATE-002: A no reaches the administrators without a name
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-RATE-002")]
[Property("UserStory", "US-HELP-06")]
public class HelpRate002Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A question rated No appears in the download of the Admin, with no name of the person.")]
    public async Task ANoReachesTheAdministratorsWithoutAName()
    {
        // In scope for the manual and asked by no other test, so the only record it can leave is the one the rating makes.
        var question = "How do I create a task before the deadline";
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
        // 4. Type "How do I create a task before the deadline" in the "Your question" field.
        await help.TypeQuestion(question);
        // 5. Click the "Ask" button.
        await help.ClickAsk();
        // 6. Click the "No" button.
        await help.ClickNo();

        // 7. As Admin: Open the Unanswered questions page.
        await adminQuestions.Open();
        // 8. As Admin: Type "2020-01-01" in the "From" field.
        await adminQuestions.TypeFrom("2020-01-01");
        // 9. As Admin: Type "2099-12-31" in the "To" field.
        await adminQuestions.TypeTo("2099-12-31");
        // 10. As Admin: Click the "Download" button.
        await adminQuestions.ClickDownload();
        var file = await adminQuestions.DownloadedFile();

        // The message "Thanks for your feedback." appears.
        await Expect(help.Text("Thanks for your feedback.")).ToBeVisibleAsync();
        // As Admin: the downloaded file has an entry with the question "How do I create a task before the deadline" and the reason "not_helpful".
        var entry = file.EntryFor(question);
        Assert.That(entry, Is.Not.Null, "The downloaded file has no entry with the question.");
        Assert.That(entry!.Reason, Is.EqualTo("not_helpful"));
        // As Admin: the entry shows no name of a person.
        Assert.That(file.Text, Does.Not.Contain("Dev Member"));
    }
}
