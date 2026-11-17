using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-RATE-004: The fixed reply also asks whether it helped
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-RATE-004")]
[Property("UserStory", "US-HELP-06")]
public class HelpRate004Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("Under the fixed reply for an out-of-scope question the panel asks Did this help.")]
    public async Task TheFixedReplyAlsoAsksWhetherItHelped()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Help" button.
        await help.ClickHelp();
        // 3. Click the "Ask" tab.
        await help.ClickAskTab();
        // 4. Type "Can I pay my invoice from here?" in the "Your question" field.
        await help.TypeQuestion("Can I pay my invoice from here?");
        // 5. Click the "Ask" button.
        await help.ClickAsk();

        // The message "The manual has nothing on this yet." appears.
        await Expect(help.Text("The manual has nothing on this yet.")).ToBeVisibleAsync();
        // Under it the panel asks "Did this help?" with the buttons "Yes" and "No".
        await Expect(help.Text("Did this help?")).ToBeVisibleAsync();
        await Expect(help.AskButtonNamed("Yes")).ToBeVisibleAsync();
        await Expect(help.AskButtonNamed("No")).ToBeVisibleAsync();
    }
}
