using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-RATE-003: A yes is not passed on to the administrators
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-RATE-003")]
[Property("UserStory", "US-HELP-06")]
public class HelpRate003Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A question rated Yes does not appear in the download of the Admin.")]
    public async Task AYesIsNotPassedOnToTheAdministrators()
    {
        // In scope for the manual and asked by no other test, so the only record it can leave is the one the rating makes.
        var question = "How do I delete a task that I created";
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
        // 4. Type "How do I delete a task that I created" in the "Your question" field.
        await help.TypeQuestion(question);
        // 5. Click the "Ask" button.
        await help.ClickAsk();
        // 6. Click the "Yes" button.
        await help.ClickYes();

        // 7. As Admin: Open the Unanswered questions page.
        await adminQuestions.Open();
        // 8. As Admin: Type "2020-01-01" in the "From" field.
        await adminQuestions.TypeFrom("2020-01-01");
        // 9. As Admin: Type "2099-12-31" in the "To" field.
        await adminQuestions.TypeTo("2099-12-31");
        // 10. As Admin: Click the "Download" button.
        await adminQuestions.ClickDownload();
        var file = await adminQuestions.DownloadedFile();

        // As Admin: the downloaded file has no entry with the question "How do I delete a task that I created".
        Assert.That(file.EntryFor(question), Is.Null, "A question rated Yes must not reach the administrators.");
    }
}
