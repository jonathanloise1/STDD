using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-STATUS-002: The status goes round from Todo to Done and back
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-STATUS-002")]
[Property("UserStory", "US-TASK-03")]
public class TaskStatus002Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Each click moves the task to the next status; after 'Done' the task is back to 'Todo'.")]
    public async Task TheStatusGoesRoundFromTodoToDoneAndBack()
    {
        var title = UniqueTitle("Archive old reports");
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
        // 6. Click the "Change status" button on the row of the task.
        await tasks.ClickChangeStatus(title);
        // After the second click the row shows the status "Done".
        await Expect(tasks.StatusOf(title)).ToHaveTextAsync("Done");
        // 7. Click the "Change status" button on the row of the task.
        await tasks.ClickChangeStatus(title);

        // After the third click the row shows the status "Todo".
        await Expect(tasks.StatusOf(title)).ToHaveTextAsync("Todo");
    }
}
