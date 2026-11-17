using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-CREATE-001: Create a task with a title
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-CREATE-001")]
[Property("UserStory", "US-TASK-01")]
public class TaskCreate001Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("An editor adds a task with a title, a due date and a description, and finds it in the list.")]
    public async Task CreateATaskWithATitle()
    {
        var title = UniqueTitle("Prepare the quarterly report");
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type the title in the "Title" field.
        await tasks.TypeTitle(title);
        // 4. Type "2030-01-15" in the "Due Date" field.
        await tasks.TypeDueDate("2030-01-15");
        // 5. Type the description in the "Description" field.
        await tasks.TypeDescription("Collect the figures from every team");
        // 6. Click the "Create Task" button.
        await tasks.ClickCreateTask();

        // The form closes.
        await Expect(tasks.TitleField).ToBeHiddenAsync();
        // The first row shows the title with the description.
        await Expect(tasks.FirstRow).ToContainTextAsync(title);
        await Expect(tasks.TitleCellOf(title)).ToContainTextAsync("Collect the figures from every team");
        // The first row shows the status "Todo" and the due date "2030-01-15".
        await Expect(tasks.StatusOf(title)).ToHaveTextAsync("Todo");
        await Expect(tasks.DueDateOf(title)).ToHaveTextAsync("2030-01-15");
    }
}
