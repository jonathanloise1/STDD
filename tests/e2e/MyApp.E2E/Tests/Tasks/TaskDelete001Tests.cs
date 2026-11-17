using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-DELETE-001: Delete a task after confirming
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-DELETE-001")]
[Property("UserStory", "US-TASK-04")]
public class TaskDelete001Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("The 'Delete' button asks for confirmation; confirming removes the task from the list.")]
    public async Task DeleteATaskAfterConfirming()
    {
        var title = UniqueTitle("Remove the old banner");
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type the title in the "Title" field.
        await tasks.TypeTitle(title);
        // 4. Click the "Create Task" button.
        await tasks.ClickCreateTask();
        // 5. Click the "Delete" button on the row of the task.
        await tasks.ClickDelete(title);
        // After step 5, the "Delete Task" dialog asks "Are you sure you want to delete this task?"
        await Expect(tasks.DeleteDialog).ToContainTextAsync("Are you sure you want to delete this task?");
        // 6. Click the "Delete" button in the dialog.
        await tasks.ClickDeleteInDialog();

        // After step 6, the dialog is closed.
        await Expect(tasks.DeleteDialog).ToBeHiddenAsync();
        // The list does not show the title.
        await tasks.ExpectNotListed(title);
    }
}
