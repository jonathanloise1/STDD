using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-RECENT-003: Loading recent tasks when the service does not answer shows a message
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("TestCase", "DASH-RECENT-003")]
[Property("UserStory", "US-DASH-02")]
public class DashRecent003Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.StatsViewer;

    [Test]
    [Description("The section says the service did not answer instead of claiming there are no tasks.")]
    public async Task LoadingRecentTasksWhenTheServiceDoesNotAnswerShowsAMessage()
    {
        var dashboard = new DashboardPage(Page);
        ServiceDoesNotAnswer();

        // 1. Open the Dashboard page.
        await dashboard.Open();

        // The "Recent Tasks" section shows "The service did not answer. Try again in a few minutes."
        await Expect(dashboard.RecentMessage("The service did not answer. Try again in a few minutes.")).ToBeVisibleAsync();
        // The section does not show "No tasks yet".
        await Expect(dashboard.RecentNoTasksText).ToHaveCountAsync(0);
    }
}
