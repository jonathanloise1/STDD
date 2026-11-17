using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Manual.Tasks;

/// <summary>Screenshots of the Tasks page for the manual (manual.md 4).</summary>
[TestFixture]
[Category("tasks")]
[Category("manual-capture")]
[Property("UserStory", "US-TASK-01")]
[Property("UserStory", "US-TASK-02")]
[Property("UserStory", "US-TASK-03")]
[Property("UserStory", "US-TASK-04")]
[Property("UserStory", "US-TASK-05")]
public class TasksCapture : ManualCaptureBase
{
    // Tasks that the seed data puts in "MyApp Dev Org".
    private const string SeedTask = "Design database schema";

    protected override TestUser GetTestUser(string state) => state switch
    {
        nameof(NoTasks) => TestUsers.EmptyAdmin,
        nameof(DefaultAsViewer) => TestUsers.ViewerUser,
        _ => TestUsers.MemberUser,
    };

    [Test]
    public async Task Default()
    {
        var tasks = new TasksPage(Page);
        await tasks.Open();
        await ShootAsync(tasks);
    }

    [Test]
    public async Task DefaultAsViewer()
    {
        var tasks = new TasksPage(Page);
        await tasks.Open();
        await ShootAsync(tasks);
    }

    [Test]
    public async Task NoTasks()
    {
        var tasks = new TasksPage(Page);
        await tasks.Open();
        await ShootAsync(tasks);
    }

    [Test]
    public async Task NewTaskFormOpen()
    {
        var tasks = new TasksPage(Page);
        await tasks.Open();
        await tasks.ClickNewTask();
        await ShootAsync(tasks);
    }

    [Test]
    public async Task NewTaskTitleMissing()
    {
        var tasks = new TasksPage(Page);
        await tasks.Open();
        await tasks.ClickNewTask();
        await tasks.ClickCreateTask();
        await Expect(tasks.FieldMessage("Title")).ToBeVisibleAsync();
        await ShootAsync(tasks);
    }

    [Test]
    public async Task EditTaskFormOpen()
    {
        var tasks = new TasksPage(Page);
        await tasks.Open();
        await tasks.ClickEdit(SeedTask);
        await ShootAsync(tasks);
    }

    [Test]
    public async Task DeleteTaskDialogOpen()
    {
        var tasks = new TasksPage(Page);
        await tasks.Open();
        await tasks.ClickDelete(SeedTask);
        await ShootAsync(tasks);
    }

    [Test]
    public async Task ListUnavailable()
    {
        var tasks = new TasksPage(Page);
        ServiceDoesNotAnswer("TASK-LIST-004");
        await tasks.Open();
        await WaitForMessageAsync();
        await ShootAsync(tasks);
    }

    [Test]
    public async Task SaveUnavailable()
    {
        var tasks = new TasksPage(Page);
        ServiceDoesNotAnswer("TASK-CREATE-008");
        await tasks.Open();
        await tasks.ClickNewTask();
        await tasks.TypeTitle("Book the training room");
        await tasks.ClickCreateTask();
        await WaitForMessageAsync();
        await ShootAsync(tasks);
    }
}
