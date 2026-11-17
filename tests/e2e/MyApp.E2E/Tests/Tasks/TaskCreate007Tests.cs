using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-CREATE-007: A viewer cannot create tasks
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-CREATE-007")]
[Property("UserStory", "US-TASK-01")]
public class TaskCreate007Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.ViewerUser;

    [Test]
    [Description("A viewer sees the tasks of the organization but is offered no way to add one.")]
    public async Task AViewerCannotCreateTasks()
    {
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();

        // The list shows "Set up CI/CD pipeline".
        await Expect(tasks.RowFor("Set up CI/CD pipeline")).ToBeVisibleAsync();
        // No "New Task" button is shown.
        await Expect(tasks.NewTaskButton).ToHaveCountAsync(0);
    }
}
