using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-LIST-005: An organization without tasks shows an empty message
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-LIST-005")]
[Property("UserStory", "US-TASK-05")]
public class TaskList005Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.EmptyAdmin;

    [Test]
    [Description("When the organization has no tasks, the page says so and invites an admin to create the first one.")]
    public async Task AnOrganizationWithoutTasksShowsAnEmptyMessage()
    {
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();

        // The page shows "No tasks yet" and "Create your first task to get started."
        await Expect(tasks.EmptyMessage).ToBeVisibleAsync();
        await Expect(tasks.EmptyHint).ToBeVisibleAsync();
        // No table of tasks is shown.
        await Expect(tasks.Table).ToHaveCountAsync(0);
        // The "New Task" button is shown.
        await Expect(tasks.NewTaskButton).ToBeVisibleAsync();
    }
}
