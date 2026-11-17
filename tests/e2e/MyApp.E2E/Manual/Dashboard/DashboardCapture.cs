using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;

namespace MyApp.E2E.Manual.Dashboard;

/// <summary>Screenshots of the Dashboard page for the manual (manual.md 4).</summary>
[TestFixture]
[Category("dashboard")]
[Category("manual-capture")]
[Property("UserStory", "US-DASH-01")]
[Property("UserStory", "US-DASH-02")]
[Property("UserStory", "US-DASH-03")]
[Property("UserStory", "US-DASH-04")]
public class DashboardCapture : ManualCaptureBase
{
    protected override TestUser GetTestUser(string state) =>
        state == nameof(NoTasks) ? TestUsers.EmptyAdmin : TestUsers.AdminUser;

    [Test]
    public async Task Default()
    {
        var dashboard = new DashboardPage(Page);
        await dashboard.Open();
        await Expect(dashboard.Greeting).ToBeVisibleAsync();
        await ShootAsync(dashboard);
    }

    [Test]
    public async Task DefaultMobile()
    {
        var dashboard = new DashboardPage(Page);
        await dashboard.Open();
        await Expect(dashboard.Greeting).ToBeVisibleAsync();
        await ShootAsync(dashboard);
    }

    [Test]
    public async Task NoTasks()
    {
        var dashboard = new DashboardPage(Page);
        await dashboard.Open();
        await Expect(dashboard.Greeting).ToBeVisibleAsync();
        await ShootAsync(dashboard);
    }

    [Test]
    public async Task CountsUnavailable()
    {
        var dashboard = new DashboardPage(Page);
        ServiceDoesNotAnswer("DASH-KPI-004");
        await dashboard.Open();
        await WaitForMessageAsync();
        await ShootAsync(dashboard);
    }

    [Test]
    public async Task RecentTasksUnavailable()
    {
        var dashboard = new DashboardPage(Page);
        ServiceDoesNotAnswer("DASH-RECENT-003");
        await dashboard.Open();
        await WaitForMessageAsync();
        await ShootAsync(dashboard);
    }
}
