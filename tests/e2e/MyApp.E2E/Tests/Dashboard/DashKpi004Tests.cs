using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-KPI-004: Loading the counts when the service does not answer shows a message
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("TestCase", "DASH-KPI-004")]
[Property("UserStory", "US-DASH-01")]
public class DashKpi004Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.StatsViewer;

    [Test]
    [Description("A message replaces the cards instead of zeros.")]
    public async Task LoadingTheCountsWhenTheServiceDoesNotAnswerShowsAMessage()
    {
        var dashboard = new DashboardPage(Page);
        ServiceDoesNotAnswer();

        // 1. Open the Dashboard page.
        await dashboard.Open();

        // The message "The service did not answer. Try again in a few minutes." is shown in place of the cards.
        await Expect(dashboard.Message("The service did not answer. Try again in a few minutes.")).ToBeVisibleAsync();
        // The card "Total Tasks" does not show 0.
        await Expect(dashboard.Card("Total Tasks")).ToHaveCountAsync(0);
    }
}
