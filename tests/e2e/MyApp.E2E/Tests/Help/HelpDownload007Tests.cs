using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Help;

namespace MyApp.E2E.Tests.Help;

/// <summary>
/// HELP-DOWNLOAD-007: An editor cannot see or open the unanswered questions
/// </summary>
[TestFixture]
[Category("help")]
[Property("TestCase", "HELP-DOWNLOAD-007")]
[Property("UserStory", "US-HELP-07")]
public class HelpDownload007Tests : HelpTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("An Editor has no menu item and is refused when opening the page by its address.")]
    public async Task AnEditorCannotSeeOrOpenTheUnansweredQuestions()
    {
        var dashboard = new DashboardPage(Page);
        var sidebar = new SidebarPage(Page);
        var questions = new UnansweredQuestionsPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();
        // The menu does not show "Unanswered questions".
        await Expect(sidebar.Entry("Unanswered questions")).ToHaveCountAsync(0);
        // 2. Open the Unanswered questions page.
        await questions.Open();

        // The message "You don't have access to this page." appears.
        await Expect(questions.Message("You don't have access to this page.")).ToBeVisibleAsync();
        // The "Download" button is not shown.
        await Expect(questions.DownloadButton).ToHaveCountAsync(0);
    }
}
