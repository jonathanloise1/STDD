using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-STATUS-005: A viewer cannot change the status of a task
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-STATUS-005")]
[Property("UserStory", "US-TASK-03")]
public class TaskStatus005Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.ViewerUser;

    [Test]
    [Description("A viewer sees the status of each task but no row offers a 'Change status' button.")]
    public async Task AViewerCannotChangeTheStatusOfATask()
    {
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();

        // The row of "Set up CI/CD pipeline" shows the status "Done".
        await Expect(tasks.StatusOf("Set up CI/CD pipeline")).ToHaveTextAsync("Done");
        // No row shows a "Change status" button.
        await Expect(tasks.ChangeStatusButtons).ToHaveCountAsync(0);
    }
}
