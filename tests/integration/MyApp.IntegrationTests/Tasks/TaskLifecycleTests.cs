using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Tasks;

/// <summary>
/// Creating, reading, changing and deleting tasks. Moved from the old TaskCrud001Tests and TaskList001Tests;
/// the data is a new organization, so the demo tasks of the seed data are never touched.
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("UserStory", "US-TASK-01")]
[Property("UserStory", "US-TASK-02")]
[Property("UserStory", "US-TASK-03")]
[Property("UserStory", "US-TASK-04")]
[Property("UserStory", "US-TASK-05")]
public class TaskLifecycleTests
{
    private Guid _organizationId;
    private Person _admin = null!;

    [SetUp]
    public async Task NewOrganization() => (_organizationId, _admin) = await TestWorld.CreateOrganization();

    private string Tasks => TestWorld.TasksPath(_organizationId);

    [Test]
    [Description("A new task is returned with status Todo, its title, description and due date")]
    public async Task Create_ReturnsTheTaskWithStatusTodo()
    {
        var response = await Api.Post(Tasks, new { title = "Prepare the quarterly report", description = "Collect the figures", dueDate = "2030-01-15" }, _admin.Id);

        ApiAssert.IsSuccess(response, 201);
        Assert.That(Guid.TryParse(response.String("id"), out _), Is.True);
        Assert.That(response.String("title"), Is.EqualTo("Prepare the quarterly report"));
        Assert.That(response.String("description"), Is.EqualTo("Collect the figures"));
        Assert.That(response.String("dueDate"), Is.EqualTo("2030-01-15"));
        Assert.That(response.String("status"), Is.EqualTo("Todo"));
    }

    [Test]
    [Description("The list shows the newest task first and counts every task of the organization")]
    public async Task List_ShowsTheNewestFirst()
    {
        await TestWorld.CreateTask(_organizationId, _admin, "Older task");
        await Task.Delay(50);
        await TestWorld.CreateTask(_organizationId, _admin, "Newer task");

        var response = await Api.Get(Tasks, _admin.Id);

        ApiAssert.IsSuccess(response);
        Assert.That(response.Json.GetProperty("totalCount").GetInt32(), Is.EqualTo(2));
        var titles = response.Json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()).ToList();
        Assert.That(titles, Is.EqualTo(new[] { "Newer task", "Older task" }));
    }

    [Test]
    [Description("A task is read back by its id")]
    public async Task GetById_ReturnsTheTask()
    {
        var id = await TestWorld.CreateTask(_organizationId, _admin, "Read me back");

        var response = await Api.Get($"{Tasks}/{id}", _admin.Id);

        ApiAssert.IsSuccess(response);
        Assert.That(response.String("title"), Is.EqualTo("Read me back"));
    }

    [Test]
    [Description("A body with only the status changes only the status: title, description and due date are kept")]
    public async Task UpdateWithOnlyTheStatus_KeepsTheOtherDetails()
    {
        var id = await TestWorld.CreateTask(_organizationId, _admin, "Order business cards", "Ask the printer for a quote", "2030-03-10");

        var response = await Api.Put($"{Tasks}/{id}", new { status = "InProgress" }, _admin.Id);

        ApiAssert.IsSuccess(response);
        Assert.That(response.String("status"), Is.EqualTo("InProgress"));
        Assert.That(response.String("title"), Is.EqualTo("Order business cards"));
        Assert.That(response.String("description"), Is.EqualTo("Ask the printer for a quote"));
        Assert.That(response.String("dueDate"), Is.EqualTo("2030-03-10"));
    }

    [Test]
    [Description("A body with a title and a due date changes them and keeps the status and description")]
    public async Task UpdateWithTitleAndDueDate_KeepsTheRest()
    {
        var id = await TestWorld.CreateTask(_organizationId, _admin, "Draft the budget", "Use last year's figures");

        var response = await Api.Put($"{Tasks}/{id}", new { title = "Draft the yearly budget", dueDate = "2030-02-01" }, _admin.Id);

        ApiAssert.IsSuccess(response);
        Assert.That(response.String("title"), Is.EqualTo("Draft the yearly budget"));
        Assert.That(response.String("dueDate"), Is.EqualTo("2030-02-01"));
        Assert.That(response.String("description"), Is.EqualTo("Use last year's figures"));
        Assert.That(response.String("status"), Is.EqualTo("Todo"));
    }

    [Test]
    [Description("Choosing Cancelled in the edit sets that status; clearing the due date removes it")]
    public async Task Update_CanCancelAndClearTheDueDate()
    {
        var id = await TestWorld.CreateTask(_organizationId, _admin, "Order new monitors", null, "2030-05-05");

        var response = await Api.Put($"{Tasks}/{id}", new { status = "Cancelled", clearDueDate = true }, _admin.Id);

        ApiAssert.IsSuccess(response);
        Assert.That(response.String("status"), Is.EqualTo("Cancelled"));
        Assert.That(response.Json.GetProperty("dueDate").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
    }

    [Test]
    [Description("A deleted task is gone: reading it answers 404 and the list no longer counts it")]
    public async Task Delete_RemovesTheTask()
    {
        var id = await TestWorld.CreateTask(_organizationId, _admin, "Remove the old banner");

        var delete = await Api.Delete($"{Tasks}/{id}", _admin.Id);
        ApiAssert.IsSuccess(delete, 204);

        ApiAssert.IsProblem(await Api.Get($"{Tasks}/{id}", _admin.Id), 404);
        var list = await Api.Get(Tasks, _admin.Id);
        Assert.That(list.Json.GetProperty("totalCount").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    [Description("A task of another organization cannot be read through this one")]
    public async Task Task_OfAnotherOrganization_IsNotFound()
    {
        var id = await TestWorld.CreateTask(_organizationId, _admin, "Private to this organization");
        var (otherOrganizationId, otherAdmin) = await TestWorld.CreateOrganization();

        var response = await Api.Get($"{TestWorld.TasksPath(otherOrganizationId)}/{id}", otherAdmin.Id);

        ApiAssert.IsProblem(response, 404);
    }
}
