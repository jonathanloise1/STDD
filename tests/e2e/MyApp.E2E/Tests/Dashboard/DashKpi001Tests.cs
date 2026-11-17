using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-KPI-001: The dashboard shows the task counts by status
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("TestCase", "DASH-KPI-001")]
[Property("UserStory", "US-DASH-01")]
public class DashKpi001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.StatsViewer;

    [Test]
    [Description("The four cards show the total and the tasks of each status.")]
    public async Task TheDashboardShowsTheTaskCountsByStatus()
    {
        var dashboard = new DashboardPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();

        // The card "Total Tasks" shows 12.
        await Expect(dashboard.CardValue("Total Tasks")).ToHaveTextAsync("12");
        // The card "Todo" shows 5.
        await Expect(dashboard.CardValue("Todo")).ToHaveTextAsync("5");
        // The card "In Progress" shows 3.
        await Expect(dashboard.CardValue("In Progress")).ToHaveTextAsync("3");
        // The card "Done" shows 3.
        await Expect(dashboard.CardValue("Done")).ToHaveTextAsync("3");
    }
}
