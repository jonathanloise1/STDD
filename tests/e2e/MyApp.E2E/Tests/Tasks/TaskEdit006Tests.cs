using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-EDIT-006: A viewer cannot edit tasks
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-EDIT-006")]
[Property("UserStory", "US-TASK-02")]
public class TaskEdit006Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.ViewerUser;

    [Test]
    [Description("A viewer sees the tasks but no row offers an 'Edit' button.")]
    public async Task AViewerCannotEditTasks()
    {
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();

        // The list shows "Add audit logging".
        await Expect(tasks.RowFor("Add audit logging")).ToBeVisibleAsync();
        // No row shows an "Edit" button.
        await Expect(tasks.EditButtons).ToHaveCountAsync(0);
    }
}
