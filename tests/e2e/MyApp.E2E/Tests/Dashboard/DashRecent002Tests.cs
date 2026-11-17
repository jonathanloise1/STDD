using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-RECENT-002: Only the five newest tasks are listed
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("TestCase", "DASH-RECENT-002")]
[Property("UserStory", "US-DASH-02")]
public class DashRecent002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.StatsViewer;

    [Test]
    [Description("The table shows five rows, from Stats task 12 to Stats task 08.")]
    public async Task OnlyTheFiveNewestTasksAreListed()
    {
        var dashboard = new DashboardPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();

        // The "Recent Tasks" table shows five rows.
        await Expect(dashboard.RecentRows).ToHaveCountAsync(5);
        // The first row is "Stats task 12" and the last row is "Stats task 08".
        await Expect(dashboard.RecentRows.First).ToContainTextAsync("Stats task 12");
        await Expect(dashboard.RecentRows.Last).ToContainTextAsync("Stats task 08");
        // "Stats task 07" and "Stats task 01" are not listed.
        await Expect(dashboard.RecentRowFor("Stats task 07")).ToHaveCountAsync(0);
        await Expect(dashboard.RecentRowFor("Stats task 01")).ToHaveCountAsync(0);
    }
}
