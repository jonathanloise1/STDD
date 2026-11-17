using MyApp.Infrastructure.Seed;
using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Dashboard;

/// <summary>
/// The keyed outcomes of the dashboard (operations.md 4.2), reached through the fault switch: each figure
/// fails on its own, with the key of its test case as <c>code</c>.
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("UserStory", "US-DASH-01")]
[Property("UserStory", "US-DASH-02")]
[Property("TestCase", "DASH-KPI-004")]
[Property("TestCase", "DASH-RECENT-003")]
public class DashboardOutcomeCodesTests
{
    private const string Unavailable = "The service did not answer. Try again in a few minutes.";

    [Test]
    public async Task dash_kpi_unavailable()
    {
        var response = await Api.Get($"/api/organizations/{SeedDataSet.StatsOrg}/dashboard/counts", SeedDataSet.StatsAdmin, Api.Fault("dash.kpi.unavailable"));

        ApiAssert.IsProblem(response, 503, "dash.kpi.unavailable", Unavailable);
    }

    [Test]
    public async Task dash_recent_unavailable()
    {
        var response = await Api.Get($"/api/organizations/{SeedDataSet.StatsOrg}/dashboard/recent-tasks", SeedDataSet.StatsAdmin, Api.Fault("dash.recent.unavailable"));

        ApiAssert.IsProblem(response, 503, "dash.recent.unavailable", Unavailable);
    }

    [Test]
    [Description("A fault on one figure leaves the other answering")]
    public async Task OneFigureFails_TheOtherAnswers()
    {
        var counts = await Api.Get($"/api/organizations/{SeedDataSet.StatsOrg}/dashboard/counts", SeedDataSet.StatsAdmin, Api.Fault("dash.recent.unavailable"));

        ApiAssert.IsSuccess(counts);
    }
}
