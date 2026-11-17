using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-EMPTY-002: An organization without tasks shows zero on every card
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("TestCase", "DASH-EMPTY-002")]
[Property("UserStory", "US-DASH-01")]
public class DashEmpty002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.EmptyAdmin;

    [Test]
    [Description("All four cards show 0.")]
    public async Task AnOrganizationWithoutTasksShowsZeroOnEveryCard()
    {
        var dashboard = new DashboardPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();

        // The cards "Total Tasks", "Todo", "In Progress" and "Done" all show 0.
        await Expect(dashboard.CardValue("Total Tasks")).ToHaveTextAsync("0");
        await Expect(dashboard.CardValue("Todo")).ToHaveTextAsync("0");
        await Expect(dashboard.CardValue("In Progress")).ToHaveTextAsync("0");
        await Expect(dashboard.CardValue("Done")).ToHaveTextAsync("0");
    }
}
