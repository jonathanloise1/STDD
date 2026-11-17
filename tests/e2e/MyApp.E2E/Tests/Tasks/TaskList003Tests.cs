using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-LIST-003: A missing due date is shown as a dash
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-LIST-003")]
[Property("UserStory", "US-TASK-05")]
public class TaskList003Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.ViewerUser;

    [Test]
    [Description("A task without a due date shows '-' in the Due Date column; a viewer's table has no Actions column.")]
    public async Task AMissingDueDateIsShownAsADash()
    {
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();

        // The row of "Set up monitoring dashboard" shows "-" under "Due Date".
        await Expect(tasks.DueDateOf("Set up monitoring dashboard")).ToHaveTextAsync("-");
        // The table has the columns "Title", "Status" and "Due Date" and no column "Actions".
        await Expect(tasks.ColumnHeaders).ToHaveTextAsync(new[] { "Title", "Status", "Due Date" });
        await Expect(tasks.ColumnHeader("Actions")).ToHaveCountAsync(0);
    }
}
