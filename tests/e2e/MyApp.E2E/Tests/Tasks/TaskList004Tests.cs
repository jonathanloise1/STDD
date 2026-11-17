using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-LIST-004: Loading when the service does not answer shows a message
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-LIST-004")]
[Property("UserStory", "US-TASK-05")]
public class TaskList004Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("When the service does not answer while the tasks are loaded, the page says so instead of showing an empty list.")]
    public async Task LoadingWhenTheServiceDoesNotAnswerShowsAMessage()
    {
        var tasks = new TasksPage(Page);
        // The service does not answer when the tasks are loaded.
        ServiceDoesNotAnswer();

        // 1. Open the Tasks page.
        await tasks.Open();

        // The message "The service did not answer. Try again in a few minutes." appears.
        await Expect(tasks.Message("The service did not answer. Try again in a few minutes.")).ToBeVisibleAsync();
        // The page does not show "No tasks yet".
        await Expect(tasks.EmptyMessage).ToHaveCountAsync(0);
    }
}
