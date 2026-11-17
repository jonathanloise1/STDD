using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-CREATE-002: Saving without a title shows a field message
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-CREATE-002")]
[Property("UserStory", "US-TASK-01")]
public class TaskCreate002Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A task cannot be created while the Title field is empty; the form stays open and the field says why.")]
    public async Task SavingWithoutATitleShowsAFieldMessage()
    {
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type "Task without a title" in the "Description" field.
        await tasks.TypeDescription("Task without a title");
        // 4. Click the "Create Task" button.
        await tasks.ClickCreateTask();

        // The form stays open.
        await Expect(tasks.TitleField).ToBeVisibleAsync();
        // The "Title" field shows "Enter a title."
        await Expect(tasks.FieldMessage("Title")).ToHaveTextAsync("Enter a title.");
        // The list does not show "Task without a title".
        await tasks.ExpectNotListed("Task without a title");
    }
}
