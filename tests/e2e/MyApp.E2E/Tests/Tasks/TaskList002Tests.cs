using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-LIST-002: A newly created task is at the top of the list
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-LIST-002")]
[Property("UserStory", "US-TASK-05")]
public class TaskList002Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("The newest task is shown first, above older tasks.")]
    public async Task ANewlyCreatedTaskIsAtTheTopOfTheList()
    {
        var title = UniqueTitle("Newest task on top");
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type the title in the "Title" field.
        await tasks.TypeTitle(title);
        // 4. Click the "Create Task" button.
        await tasks.ClickCreateTask();

        // The first row of the list shows the title.
        await Expect(tasks.FirstRow).ToContainTextAsync(title);
        // The rows below show the older tasks, for example "Review security configuration".
        await Expect(tasks.RowFor("Review security configuration")).ToBeVisibleAsync();
        await Expect(tasks.FirstRow).Not.ToContainTextAsync("Review security configuration");
    }
}
