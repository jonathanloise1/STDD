using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ANSWER-007: Declining the large download keeps the section cards
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ANSWER-007")]
[Property("UserStory", "US-HELP-05")]
public class HelpAnswer007Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("Before a large first download the panel asks, and declining still gives the best matches.")]
    public async Task DecliningTheLargeDownloadKeepsTheSectionCards()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);
        // The device can run the assistant and the first download is 120 MB.
        await TheDeviceCanRunTheAssistantAndTheFirstDownloadIs(120);

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

        // The message "Download the assistant to this device (120 MB)? Answers will then be generated on this device."
        // appears with the buttons "Download" and "Not now".
        await Expect(help.Text("Download the assistant to this device (120 MB)? Answers will then be generated on this device.")).ToBeVisibleAsync();
        await Expect(help.AskButtonNamed("Download")).ToBeVisibleAsync();
        await Expect(help.AskButtonNamed("Not now")).ToBeVisibleAsync();

        // 6. Click the "Not now" button.
        await help.ClickNotNow();

        // After "Not now", the section cards are shown under the header "Best matches in the manual".
        await Expect(help.BestMatchesHeader).ToBeVisibleAsync();
        await Expect(help.SectionCards.First).ToBeVisibleAsync();
    }
}
