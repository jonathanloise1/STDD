using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-STATUS-003: A cancelled task goes back to Todo
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-STATUS-003")]
[Property("UserStory", "US-TASK-03")]
public class TaskStatus003Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("Clicking the 'Change status' button of a cancelled task changes its status to 'Todo'.")]
    public async Task ACancelledTaskGoesBackToTodo()
    {
        var title = UniqueTitle("Replace the printer");
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type the title in the "Title" field.
        await tasks.TypeTitle(title);
        // 4. Click the "Create Task" button.
        await tasks.ClickCreateTask();
        // 5. Click the "Edit" button on the row of the task.
        await tasks.ClickEdit(title);
        // 6. Select "Cancelled" in the "Status" field.
        await tasks.SelectStatus("Cancelled");
        // 7. Click the "Update" button.
        await tasks.ClickUpdate();
        await Expect(tasks.StatusOf(title)).ToHaveTextAsync("Cancelled");
        // 8. Click the "Change status" button on the row of the task.
        await tasks.ClickChangeStatus(title);

        // The row of the task shows the status "Todo".
        await Expect(tasks.StatusOf(title)).ToHaveTextAsync("Todo");
    }
}
