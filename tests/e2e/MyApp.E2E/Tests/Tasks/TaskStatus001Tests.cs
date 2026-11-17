using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-STATUS-001: Move a task from Todo to In Progress
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-STATUS-001")]
[Property("UserStory", "US-TASK-03")]
public class TaskStatus001Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("Clicking the 'Change status' button of a task with status 'Todo' changes its status to 'In Progress'.")]
    public async Task MoveATaskFromTodoToInProgress()
    {
        var title = UniqueTitle("Send the weekly update");
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type the title in the "Title" field.
        await tasks.TypeTitle(title);
        // 4. Click the "Create Task" button.
        await tasks.ClickCreateTask();
        // 5. Click the "Change status" button on the row of the task.
        await tasks.ClickChangeStatus(title);

        // The row of the task shows the status "In Progress".
        await Expect(tasks.StatusOf(title)).ToHaveTextAsync("In Progress");
    }
}
