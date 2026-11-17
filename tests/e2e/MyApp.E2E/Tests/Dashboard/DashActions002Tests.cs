using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-ACTIONS-002: Clicking a recent task row opens the task list
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("TestCase", "DASH-ACTIONS-002")]
[Property("UserStory", "US-DASH-03")]
public class DashActions002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.StatsViewer;

    [Test]
    [Description("A click on a row of Recent Tasks opens the Tasks page.")]
    public async Task ClickingARecentTaskRowOpensTheTaskList()
    {
        var dashboard = new DashboardPage(Page);
        var sidebar = new SidebarPage(Page);
        var tasks = new TasksPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();
        // 2. Click the row of "Stats task 12" in the "Recent Tasks" table.
        await dashboard.ClickRecentTask("Stats task 12");

        // The Tasks page opens.
        await Expect(sidebar.ActiveEntry).ToHaveAccessibleNameAsync("Tasks");
        // The list shows "Stats task 12".
        await Expect(tasks.RowFor("Stats task 12")).ToBeVisibleAsync();
    }
}
