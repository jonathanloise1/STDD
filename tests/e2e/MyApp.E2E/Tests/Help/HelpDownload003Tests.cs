using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-DOWNLOAD-003: E-mail addresses, web addresses and numbers are replaced
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-DOWNLOAD-003")]
[Property("UserStory", "US-HELP-07")]
public class HelpDownload003Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("Personal details typed in a question are replaced in the downloaded file.")]
    public async Task EMailAddressesWebAddressesAndNumbersAreReplaced()
    {
        var question = UniqueQuestion("can i pay invoice 12345678 from here, write to anna.rossi@example.com or see https://example.com/pay");
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
        // 4. Type "can i pay invoice 12345678 from here, write to anna.rossi@example.com or see https://example.com/pay" in the "Your question" field.
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

        // As Admin: the downloaded file has an entry with the question "can i pay invoice [number] from here, write to [email] or see [url]".
        var masked = UniqueQuestion("can i pay invoice [number] from here, write to [email] or see [url]");
        Assert.That(file.EntryFor(masked), Is.Not.Null, "The downloaded file has no entry with the masked question.");
        // As Admin: the file does not contain "12345678", "anna.rossi@example.com" or "https://example.com/pay".
        Assert.That(file.Text, Does.Not.Contain("12345678"));
        Assert.That(file.Text, Does.Not.Contain("anna.rossi@example.com"));
        Assert.That(file.Text, Does.Not.Contain("https://example.com/pay"));
    }
}
