using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-EDIT-001: Edit the title and due date of a task
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-EDIT-001")]
[Property("UserStory", "US-TASK-02")]
public class TaskEdit001Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("An editor changes the title and the due date of a task and sees the new values in its row; the description is kept.")]
    public async Task EditTheTitleAndDueDateOfATask()
    {
        var title = UniqueTitle("Draft the budget");
        var newTitle = UniqueTitle("Draft the yearly budget");
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type the title in the "Title" field.
        await tasks.TypeTitle(title);
        // 4. Type "Use last year's figures" in the "Description" field.
        await tasks.TypeDescription("Use last year's figures");
        // 5. Click the "Create Task" button.
        await tasks.ClickCreateTask();
        // 6. Click the "Edit" button on the row of the task.
        await tasks.ClickEdit(title);
        // 7. Clear the "Title" field.
        await tasks.ClearTitle();
        // 8. Type the new title in the "Title" field.
        await tasks.TypeTitle(newTitle);
        // 9. Type "2030-02-01" in the "Due Date" field.
        await tasks.TypeDueDate("2030-02-01");
        // 10. Click the "Update" button.
        await tasks.ClickUpdate();

        // The form closes.
        await Expect(tasks.TitleField).ToBeHiddenAsync();
        // The list shows a row with the new title, the due date "2030-02-01" and the description.
        await Expect(tasks.RowFor(newTitle)).ToBeVisibleAsync();
        await Expect(tasks.DueDateOf(newTitle)).ToHaveTextAsync("2030-02-01");
        await Expect(tasks.TitleCellOf(newTitle)).ToContainTextAsync("Use last year's figures");
        // The list does not show the old title.
        await tasks.ExpectNotListed(title);
    }
}
