using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-CREATE-003: A new task starts with status Todo
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-CREATE-003")]
[Property("UserStory", "US-TASK-01")]
public class TaskCreate003Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Whatever the user types in the form, a new task is shown with the status 'Todo'.")]
    public async Task ANewTaskStartsWithStatusTodo()
    {
        var title = UniqueTitle("Book the meeting room");
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // The "New Task" form has no Status field (checked while the form is open).
        await Expect(tasks.TitleField).ToBeVisibleAsync();
        await Expect(tasks.StatusField).ToHaveCountAsync(0);
        // 3. Type the title in the "Title" field.
        await tasks.TypeTitle(title);
        // 4. Click the "Create Task" button.
        await tasks.ClickCreateTask();

        // The first row of the list shows the title, with the status "Todo".
        await Expect(tasks.FirstRow).ToContainTextAsync(title);
        await Expect(tasks.StatusOf(title)).ToHaveTextAsync("Todo");
    }
}
