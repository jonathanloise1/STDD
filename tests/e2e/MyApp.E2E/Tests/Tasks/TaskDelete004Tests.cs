using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-DELETE-004: Cancelling the confirmation keeps the task
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-DELETE-004")]
[Property("UserStory", "US-TASK-04")]
public class TaskDelete004Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("Clicking 'Cancel' in the 'Delete Task' dialog closes it and the task stays in the list.")]
    public async Task CancellingTheConfirmationKeepsTheTask()
    {
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Delete" button on the row of "Add audit logging".
        await tasks.ClickDelete("Add audit logging");
        // 3. Click the "Cancel" button in the dialog.
        await tasks.ClickCancelInDialog();

        // The dialog is closed.
        await Expect(tasks.DeleteDialog).ToBeHiddenAsync();
        // The list still shows "Add audit logging".
        await Expect(tasks.RowFor("Add audit logging")).ToBeVisibleAsync();
    }
}
