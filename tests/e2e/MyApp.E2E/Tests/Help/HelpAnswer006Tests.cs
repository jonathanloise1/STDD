using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ANSWER-006: A download in progress is shown while sections answer
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ANSWER-006")]
[Property("UserStory", "US-HELP-05")]
public class HelpAnswer006Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("While the assistant is prepared on the device, the panel shows the progress and the section cards.")]
    public async Task TheDownloadInProgressIsShownWhileSectionsAnswer()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);
        // The device can run the assistant and the download is at 30 of 120 MB.
        await TheDeviceCanRunTheAssistantAndTheDownloadIsAt(30, 120);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Help" button.
        await help.ClickHelp();
        // 3. Click the "Ask" tab.
        await help.ClickAskTab();
        // 4. Type "How do I create a task" in the "Your question" field.
        await help.TypeQuestion("How do I create a task");
        // 5. Click the "Ask" button.
        await help.ClickAsk();

        // The message "Preparing the assistant on this device (30 of 120 MB)" appears.
        await Expect(help.Text("Preparing the assistant on this device (30 of 120 MB)")).ToBeVisibleAsync();
        // The section cards are shown under the header "Best matches in the manual".
        await Expect(help.BestMatchesHeader).ToBeVisibleAsync();
        await Expect(help.SectionCards.First).ToBeVisibleAsync();
    }
}
