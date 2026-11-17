using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Help;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ASK-002: The question field accepts at most 300 characters
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ASK-002")]
[Property("UserStory", "US-HELP-04")]
public class HelpAsk002Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("Typing more than 300 characters keeps only the first 300 in the field.")]
    public async Task TheQuestionFieldAcceptsAtMost300Characters()
    {
        var tasks = new TasksPage(Page);
        var help = new HelpPanelPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Help" button.
        await help.ClickHelp();
        // 3. Click the "Ask" tab.
        await help.ClickAskTab();
        // 4. Type the letter "a" 301 times in the "Your question" field.
        await help.TypeQuestion(new string('a', 301));

        // The "Your question" field shows exactly 300 characters.
        await Expect(help.QuestionField).ToHaveValueAsync(new string('a', 300));
    }
}
