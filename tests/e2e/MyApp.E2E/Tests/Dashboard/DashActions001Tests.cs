using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-ACTIONS-001: The View All button opens the task list
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("TestCase", "DASH-ACTIONS-001")]
[Property("UserStory", "US-DASH-03")]
public class DashActions001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("View All opens the Tasks page with the tasks of the organization.")]
    public async Task TheViewAllButtonOpensTheTaskList()
    {
        var dashboard = new DashboardPage(Page);
        var sidebar = new SidebarPage(Page);
        var tasks = new TasksPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();
        // 2. Click the "View All" button.
        await dashboard.ClickViewAll();

        // The Tasks page opens.
        await Expect(sidebar.ActiveEntry).ToHaveAccessibleNameAsync("Tasks");
        // The list shows "Set up CI/CD pipeline".
        await Expect(tasks.RowFor("Set up CI/CD pipeline")).ToBeVisibleAsync();
    }
}
