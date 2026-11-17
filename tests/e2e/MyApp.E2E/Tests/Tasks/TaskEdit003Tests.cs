using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-EDIT-003: Choosing Cancelled in the form changes the row status
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-EDIT-003")]
[Property("UserStory", "US-TASK-02")]
public class TaskEdit003Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("The status chosen in the edit form is the status shown on the row after updating.")]
    public async Task ChoosingCancelledInTheFormChangesTheRowStatus()
    {
        var title = UniqueTitle("Order new monitors");
        var tasks = new TasksPage(Page);

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
        // 6. Select "Cancelled" in the "Status" field.
        await tasks.SelectStatus("Cancelled");
        // 7. Click the "Update" button.
        await tasks.ClickUpdate();

        // The row of the task shows the status "Cancelled".
        await Expect(tasks.StatusOf(title)).ToHaveTextAsync("Cancelled");
    }
}
