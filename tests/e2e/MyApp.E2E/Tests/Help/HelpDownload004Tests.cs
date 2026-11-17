using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-DOWNLOAD-004: The downloaded entries show no person and no organization
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-DOWNLOAD-004")]
[Property("UserStory", "US-HELP-07")]
public class HelpDownload004Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("An entry of the file does not say who asked the question or for which organization.")]
    public async Task TheDownloadedEntriesShowNoPersonAndNoOrganization()
    {
        var question = UniqueQuestion("can i book a meeting room from here");
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
        // 4. Type "can i book a meeting room from here" in the "Your question" field.
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

        // As Admin: the downloaded file has an entry with the question "can i book a meeting room from here".
        Assert.That(file.EntryFor(question), Is.Not.Null, "The downloaded file has no entry with the question.");
        // As Admin: the file does not contain "Dev Member" or "MyApp Dev Org".
        Assert.That(file.Text, Does.Not.Contain("Dev Member"));
        Assert.That(file.Text, Does.Not.Contain("MyApp Dev Org"));
    }
}
