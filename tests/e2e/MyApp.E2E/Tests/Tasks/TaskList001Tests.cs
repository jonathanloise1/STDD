using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-LIST-001: The Tasks page lists the tasks of the organization
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-LIST-001")]
[Property("UserStory", "US-TASK-05")]
public class TaskList001Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("A signed-in admin opens the Tasks page from the menu and sees the tasks of the organization in a table.")]
    public async Task TheTasksPageListsTheTasksOfTheOrganization()
    {
        var tasks = new TasksPage(Page);
        var menu = new SidebarPage(Page);

        // 1. Click the "Tasks" link in the menu.
        await menu.ClickTasks();
        await tasks.WaitForList();

        // The Tasks page opens.
        await Expect(menu.ActiveEntry).ToHaveAccessibleNameAsync("Tasks");
        // The table has the columns "Title", "Status", "Due Date" and "Actions".
        await Expect(tasks.ColumnHeaders).ToHaveTextAsync(new[] { "Title", "Status", "Due Date", "Actions" });
        // The row of "Create task management UI" shows the status "In Progress".
        await Expect(tasks.StatusOf("Create task management UI")).ToHaveTextAsync("In Progress");
        // The row of "Set up CI/CD pipeline" shows the status "Done".
        await Expect(tasks.StatusOf("Set up CI/CD pipeline")).ToHaveTextAsync("Done");
    }
}
