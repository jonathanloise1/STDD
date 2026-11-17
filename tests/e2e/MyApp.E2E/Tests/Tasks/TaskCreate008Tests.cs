using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-CREATE-008: Saving when the service does not answer shows a message
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-CREATE-008")]
[Property("UserStory", "US-TASK-01")]
public class TaskCreate008Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("When the service does not answer while a task is saved, the editor is told so and keeps what was typed.")]
    public async Task SavingWhenTheServiceDoesNotAnswerShowsAMessage()
    {
        var title = UniqueTitle("Book the training room");
        var tasks = new TasksPage(Page);
        // The service does not answer when a task is saved.
        ServiceDoesNotAnswer();

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type the title in the "Title" field.
        await tasks.TypeTitle(title);
        // 4. Click the "Create Task" button.
        await tasks.ClickCreateTask();

        // The message "The service did not answer. Try again in a few minutes." appears.
        await Expect(tasks.Message("The service did not answer. Try again in a few minutes.")).ToBeVisibleAsync();
        // The form stays open and the "Title" field still shows the title.
        await Expect(tasks.TitleField).ToHaveValueAsync(title);
        // The list does not show the title.
        await tasks.ExpectNotListed(title);
    }
}
