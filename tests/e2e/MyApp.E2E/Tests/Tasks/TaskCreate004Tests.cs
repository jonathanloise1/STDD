using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-CREATE-004: A title made only of spaces shows a field message
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-CREATE-004")]
[Property("UserStory", "US-TASK-01")]
public class TaskCreate004Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A title made only of spaces counts as empty: the form stays open, the field says why and no task is added.")]
    public async Task ATitleMadeOnlyOfSpacesShowsAFieldMessage()
    {
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        var rowsBefore = await tasks.Rows.CountAsync();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type "   " in the "Title" field.
        await tasks.TypeTitle("   ");
        // 4. Click the "Create Task" button.
        await tasks.ClickCreateTask();

        // The form stays open.
        await Expect(tasks.TitleField).ToBeVisibleAsync();
        // The "Title" field shows "Enter a title."
        await Expect(tasks.FieldMessage("Title")).ToHaveTextAsync("Enter a title.");
        // The list does not show a new row with an empty title.
        await Expect(tasks.Rows).ToHaveCountAsync(rowsBefore);
    }
}
