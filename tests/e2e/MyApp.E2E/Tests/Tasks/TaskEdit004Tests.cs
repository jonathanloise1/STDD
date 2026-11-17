using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-EDIT-004: Updating a task deleted by someone else shows a message
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-EDIT-004")]
[Property("UserStory", "US-TASK-02")]
public class TaskEdit004Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("An editor who updates a task that another member has just deleted is told that the task no longer exists, and the list is brought up to date.")]
    public async Task UpdatingATaskDeletedBySomeoneElseShowsAMessage()
    {
        var title = UniqueTitle("Plan the offsite");
        var newTitle = UniqueTitle("Plan the team offsite");
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
        // 5. Click the "Edit" button on the row of the task.
        await tasks.ClickEdit(title);
        // 6. Clear the "Title" field.
        await tasks.ClearTitle();
        // 7. Type the new title in the "Title" field.
        await tasks.TypeTitle(newTitle);
        // 8. As Admin: Open the Tasks page.
        await adminTasks.Open();
        // 9. As Admin: Click the "Delete" button on the row of the task.
        await adminTasks.ClickDelete(title);
        // 10. As Admin: Click the "Delete" button in the dialog.
        await adminTasks.ClickDeleteInDialog();
        // 11. Click the "Update" button.
        await tasks.ClickUpdate();

        // The message "This task no longer exists." appears.
        await Expect(tasks.Message("This task no longer exists.")).ToBeVisibleAsync();
        // The list shows neither the old nor the new title.
        await tasks.ExpectNotListed(title);
        await tasks.ExpectNotListed(newTitle);
        // As Admin: the Tasks page does not list either title.
        await adminTasks.ExpectNotListed(title);
        await adminTasks.ExpectNotListed(newTitle);
    }
}
