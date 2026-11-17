using NUnit.Framework;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-ANSWER-008: A visitor cannot reach the assistant
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-ANSWER-008")]
[Property("UserStory", "US-HELP-05")]
public class HelpAnswer008Tests : HelpSignedOutTestBase
{
    [Test]
    [Description("A person who is not signed in is asked to sign in and gets no answers.")]
    public async Task AVisitorCannotReachTheAssistant()
    {
        var dashboard = new DashboardPage(Page);
        var help = new HelpPanelPage(Page);
        var signIn = new SignInPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();

        // The Sign in page opens.
        await signIn.ExpectOpen();
        // The page shows no "Help" button.
        await Expect(help.HelpButton).ToHaveCountAsync(0);
    }
}
