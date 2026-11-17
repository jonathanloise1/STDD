using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Tasks;

/// <summary>
/// The keyed outcomes of the tasks area (operations.md 4.2): the problem document carries the log key of the
/// Observable trace as its <c>code</c> and the message the user sees as its <c>detail</c>. The "service did
/// not answer" outcomes come from the fault switch (development.md 16.2).
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("UserStory", "US-TASK-01")]
[Property("UserStory", "US-TASK-02")]
[Property("UserStory", "US-TASK-03")]
[Property("UserStory", "US-TASK-04")]
[Property("UserStory", "US-TASK-05")]
[Property("TestCase", "TASK-CREATE-005")]
[Property("TestCase", "TASK-CREATE-008")]
[Property("TestCase", "TASK-DELETE-002")]
[Property("TestCase", "TASK-EDIT-004")]
[Property("TestCase", "TASK-EDIT-007")]
[Property("TestCase", "TASK-LIST-004")]
[Property("TestCase", "TASK-STATUS-004")]
public class TasksOutcomeCodesTests
{
    private const string NotAllowed = "Your role does not allow this change.";
    private const string NoLongerExists = "This task no longer exists.";
    private const string Unavailable = "The service did not answer. Try again in a few minutes.";

    private Guid _organizationId;
    private Person _admin = null!;
    private Person _viewer = null!;

    [SetUp]
    public async Task NewOrganizationWithAViewer()
    {
        (_organizationId, _admin) = await TestWorld.CreateOrganization();
        _viewer = await TestWorld.AddMember(_organizationId, _admin, "Viewer");
    }

    private string Tasks => TestWorld.TasksPath(_organizationId);

    [Test]
    public async Task task_create_not_allowed()
    {
        var response = await Api.Post(Tasks, new { title = "Not for a viewer" }, _viewer.Id);

        ApiAssert.IsProblem(response, 403, "task.create.not_allowed", NotAllowed);
    }

    [Test]
    public async Task task_edit_not_allowed()
    {
        var id = await TestWorld.CreateTask(_organizationId, _admin, "Not editable by a viewer");

        var response = await Api.Put($"{Tasks}/{id}", new { title = "Changed" }, _viewer.Id);

        ApiAssert.IsProblem(response, 403, "task.edit.not_allowed", NotAllowed);
    }

    [Test]
    public async Task task_edit_not_found()
    {
        var response = await Api.Put($"{Tasks}/{Guid.NewGuid()}", new { title = "Gone" }, _admin.Id);

        ApiAssert.IsProblem(response, 404, "task.edit.not_found", NoLongerExists);
    }

    [Test]
    public async Task task_status_not_found()
    {
        // A body with only the status is the "Change status" action
        var response = await Api.Put($"{Tasks}/{Guid.NewGuid()}", new { status = "Done" }, _admin.Id);

        ApiAssert.IsProblem(response, 404, "task.status.not_found", NoLongerExists);
    }

    [Test]
    public async Task task_delete_not_found()
    {
        var response = await Api.Delete($"{Tasks}/{Guid.NewGuid()}", _admin.Id);

        ApiAssert.IsProblem(response, 404, "task.delete.not_found", NoLongerExists);
    }

    [Test]
    public async Task task_create_unavailable()
    {
        var response = await Api.Post(Tasks, new { title = "Never saved" }, _admin.Id, Api.Fault("task.create.unavailable"));

        ApiAssert.IsProblem(response, 503, "task.create.unavailable", Unavailable);
        var list = await Api.Get(Tasks, _admin.Id);
        Assert.That(list.Json.GetProperty("totalCount").GetInt32(), Is.EqualTo(0), "The operation did not run");
    }

    [Test]
    public async Task task_list_unavailable()
    {
        var response = await Api.Get(Tasks, _admin.Id, Api.Fault("task.list.unavailable"));

        ApiAssert.IsProblem(response, 503, "task.list.unavailable", Unavailable);
    }

    [Test]
    [Description("The switch fails only the operation whose key is named")]
    public async Task TheFaultSwitch_FailsOnlyTheNamedOperation()
    {
        var response = await Api.Get(Tasks, _admin.Id, Api.Fault("task.create.unavailable"));

        ApiAssert.IsSuccess(response);
    }
}
