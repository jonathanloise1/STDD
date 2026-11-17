using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-DELETE-003: A viewer cannot delete tasks
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-DELETE-003")]
[Property("UserStory", "US-TASK-04")]
public class TaskDelete003Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.ViewerUser;

    [Test]
    [Description("A viewer sees the tasks but no row offers a 'Delete' button.")]
    public async Task AViewerCannotDeleteTasks()
    {
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();

        // The list shows "Review security configuration".
        await Expect(tasks.RowFor("Review security configuration")).ToBeVisibleAsync();
        // No row shows a "Delete" button.
        await Expect(tasks.DeleteButtons).ToHaveCountAsync(0);
    }
}
