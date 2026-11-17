using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-STATUS-004: Changing the status of a deleted task shows a message
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-STATUS-004")]
[Property("UserStory", "US-TASK-03")]
public class TaskStatus004Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("An editor who clicks the 'Change status' button of a task that another member has just deleted is told that the task no longer exists.")]
    public async Task ChangingTheStatusOfADeletedTaskShowsAMessage()
    {
        var title = UniqueTitle("Renew the licences");
        var tasks = new TasksPage(Page);
        // A second session is signed in as Admin of "MyApp Dev Org".
        var admin = await StartSecondSession(TestUsers.AdminUser);
        var adminTasks = new TasksPage(admin);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type the title in the "Title" field.
        await tasks.TypeTitle(title);
        // 4. Click the "Create Task" button.
        await tasks.ClickCreateTask();
        // 5. As Admin: Open the Tasks page.
        await adminTasks.Open();
        // 6. As Admin: Click the "Delete" button on the row of the task.
        await adminTasks.ClickDelete(title);
        // 7. As Admin: Click the "Delete" button in the dialog.
        await adminTasks.ClickDeleteInDialog();
        // 8. Click the "Change status" button on the row of the task.
        await tasks.ClickChangeStatus(title);

        // The message "This task no longer exists." appears.
        await Expect(tasks.Message("This task no longer exists.")).ToBeVisibleAsync();
        // The list does not show the title.
        await tasks.ExpectNotListed(title);
        // As Admin: the Tasks page does not list the title.
        await adminTasks.ExpectNotListed(title);
    }
}
