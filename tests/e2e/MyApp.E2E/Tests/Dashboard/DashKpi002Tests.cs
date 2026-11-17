using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-KPI-002: Cancelled tasks are counted only in the total
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("TestCase", "DASH-KPI-002")]
[Property("UserStory", "US-DASH-01")]
public class DashKpi002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.StatsViewer;

    [Test]
    [Description("The total is 12 and the three status cards add up to 11.")]
    public async Task CancelledTasksAreCountedOnlyInTheTotal()
    {
        var dashboard = new DashboardPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();

        // The card "Total Tasks" shows 12.
        await Expect(dashboard.CardValue("Total Tasks")).ToHaveTextAsync("12");
        // The cards "Todo", "In Progress" and "Done" together show 11.
        var statusCards = await dashboard.CountOf("Todo")
            + await dashboard.CountOf("In Progress")
            + await dashboard.CountOf("Done");
        Assert.That(statusCards, Is.EqualTo(11));
    }
}
