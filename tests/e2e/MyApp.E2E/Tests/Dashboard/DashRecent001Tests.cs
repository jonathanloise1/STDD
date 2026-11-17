using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Dashboard;
using MyApp.E2E.PageObjects.Common;
using System.Text.RegularExpressions;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// DASH-RECENT-001: Recent tasks show title, status and due date
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("TestCase", "DASH-RECENT-001")]
[Property("UserStory", "US-DASH-02")]
public class DashRecent001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.StatsViewer;

    [Test]
    [Description("The table shows columns, and the status and due date of three tasks.")]
    public async Task RecentTasksShowTitleStatusAndDueDate()
    {
        var dashboard = new DashboardPage(Page);

        // 1. Open the Dashboard page.
        await dashboard.Open();

        // The "Recent Tasks" section has the columns "Title", "Status" and "Due Date".
        await Expect(dashboard.RecentColumnHeaders).ToHaveTextAsync(new[] { "Title", "Status", "Due Date" });
        // The row of "Stats task 12" shows the status "Cancelled" and "-" in the "Due Date" column.
        await Expect(dashboard.RecentStatusOf("Stats task 12")).ToHaveTextAsync("Cancelled");
        await Expect(dashboard.RecentDueDateOf("Stats task 12")).ToHaveTextAsync("-");
        // The row of "Stats task 11" shows the status "Done" and a due date.
        await Expect(dashboard.RecentStatusOf("Stats task 11")).ToHaveTextAsync("Done");
        await Expect(dashboard.RecentDueDateOf("Stats task 11")).ToHaveTextAsync(new Regex(@"^(?!-$).+"));
        // The row of "Stats task 08" shows the status "In Progress".
        await Expect(dashboard.RecentStatusOf("Stats task 08")).ToHaveTextAsync("In Progress");
    }
}
