using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-STATUS-006: Changing the status keeps the other details of the task
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-STATUS-006")]
[Property("UserStory", "US-TASK-03")]
public class TaskStatus006Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("The 'Change status' button changes only the status: the description and the due date of the task are kept.")]
    public async Task ChangingTheStatusKeepsTheOtherDetailsOfTheTask()
    {
        var title = UniqueTitle("Order business cards");
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type the title in the "Title" field.
        await tasks.TypeTitle(title);
        // 4. Type "2030-03-10" in the "Due Date" field.
        await tasks.TypeDueDate("2030-03-10");
        // 5. Type "Ask the printer for a quote" in the "Description" field.
        await tasks.TypeDescription("Ask the printer for a quote");
        // 6. Click the "Create Task" button.
        await tasks.ClickCreateTask();
        // 7. Click the "Change status" button on the row of the task.
        await tasks.ClickChangeStatus(title);
        // 8. Reload the page.
        await tasks.Reload();
        await tasks.WaitForList();

        // The row of the task shows the status "In Progress".
        await Expect(tasks.StatusOf(title)).ToHaveTextAsync("In Progress");
        // The same row still shows the description and the due date.
        await Expect(tasks.TitleCellOf(title)).ToContainTextAsync("Ask the printer for a quote");
        await Expect(tasks.DueDateOf(title)).ToHaveTextAsync("2030-03-10");
    }
}
