using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-EMPTY-001: An organization without tasks shows a hint
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("TestCase", "DASH-EMPTY-001")]
[Property("UserStory", "US-DASH-02")]
public class DashEmpty001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.EmptyAdmin;

    [Test]
    [Description("The section tells an admin where to create the first task and shows no table.")]
    public async Task AnOrganizationWithoutTasksShowsAHint()
    {
        var dashboard = new DashboardPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();

        // The "Recent Tasks" section shows "No tasks yet. Go to Tasks to create your first one."
        await Expect(dashboard.RecentHint).ToBeVisibleAsync();
        // No table is shown in the section.
        await Expect(dashboard.RecentTable).ToHaveCountAsync(0);
    }
}
