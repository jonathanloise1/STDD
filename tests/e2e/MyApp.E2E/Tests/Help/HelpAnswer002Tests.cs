using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ANSWER-002: A source link opens the cited section
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ANSWER-002")]
[Property("UserStory", "US-HELP-05")]
public class HelpAnswer002Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("The source link of an answer opens the cited section in the manual.")]
    public async Task ASourceLinkOpensTheCitedSection()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);
        var manual = new ManualPage(Page);
        // The assistant is available on the server and answers "How do I create a task" citing a section of the page "Create a task" as its source.
        await ServerAssistantAnswersCitingCreateATask();

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
        // The cited section is the first card; its heading is the section the link must open.
        var citedHeading = (await help.FirstCardHeading.InnerTextAsync()).Trim();
        // 6. Click the source link.
        await help.ClickSource();

        // The Manual page shows the page "Create a task" scrolled to the cited section.
        await Expect(manual.PageTitle("Create a task")).ToBeVisibleAsync();
        await Expect(manual.SectionHeading(citedHeading)).ToBeInViewportAsync();
    }
}
