using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-CREATE-006: Cancelling closes the form without adding a task
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-CREATE-006")]
[Property("UserStory", "US-TASK-01")]
public class TaskCreate006Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("Clicking 'Cancel' closes the form and discards what was typed.")]
    public async Task CancellingClosesTheFormWithoutAddingATask()
    {
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type "Draft that will be discarded" in the "Title" field.
        await tasks.TypeTitle("Draft that will be discarded");
        // 4. Click the "Cancel" button.
        await tasks.ClickCancel();

        // The form is closed.
        await Expect(tasks.TitleField).ToBeHiddenAsync();
        // The list does not show "Draft that will be discarded".
        await tasks.ExpectNotListed("Draft that will be discarded");
    }
}
