using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Tasks;

/// <summary>
/// Who may do what with tasks: every member reads, Admins and Editors change, Viewers and outsiders do not.
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("UserStory", "US-TASK-01")]
[Property("UserStory", "US-TASK-02")]
[Property("UserStory", "US-TASK-03")]
[Property("UserStory", "US-TASK-04")]
[Property("UserStory", "US-TASK-05")]
public class TaskAccessTests
{
    private Guid _organizationId;
    private Person _admin = null!;
    private Person _viewer = null!;
    private Person _editor = null!;
    private Guid _taskId;

    [SetUp]
    public async Task NewOrganizationWithMembers()
    {
        (_organizationId, _admin) = await TestWorld.CreateOrganization();
        _viewer = await TestWorld.AddMember(_organizationId, _admin, "Viewer");
        _editor = await TestWorld.AddMember(_organizationId, _admin, "Editor");
        _taskId = await TestWorld.CreateTask(_organizationId, _admin, "Visible to every member");
    }

    private string Tasks => TestWorld.TasksPath(_organizationId);

    [Test]
    [Description("A Viewer reads the list of tasks")]
    public async Task Viewer_ReadsTheList()
    {
        var response = await Api.Get(Tasks, _viewer.Id);

        ApiAssert.IsSuccess(response);
        Assert.That(response.Json.GetProperty("totalCount").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    [Description("An Editor creates, changes and deletes tasks")]
    public async Task Editor_ChangesTasks()
    {
        var created = await Api.Post(Tasks, new { title = "Editor task" }, _editor.Id);
        ApiAssert.IsSuccess(created, 201);
        var id = created.String("id");

        ApiAssert.IsSuccess(await Api.Put($"{Tasks}/{id}", new { status = "Done" }, _editor.Id));
        ApiAssert.IsSuccess(await Api.Delete($"{Tasks}/{id}", _editor.Id), 204);
    }

    [Test]
    [Description("A Viewer cannot delete a task; it stays in the list")]
    public async Task Viewer_CannotDelete()
    {
        var response = await Api.Delete($"{Tasks}/{_taskId}", _viewer.Id);

        ApiAssert.IsProblem(response, 403);
        ApiAssert.IsSuccess(await Api.Get($"{Tasks}/{_taskId}", _admin.Id));
    }

    [Test]
    [Description("A user who is not a member sees nothing of the organization's tasks")]
    public async Task Outsider_IsRefused()
    {
        var outsider = await TestWorld.CreateUser("Outsider");

        ApiAssert.IsProblem(await Api.Get(Tasks, outsider.Id), 403);
        ApiAssert.IsProblem(await Api.Post(Tasks, new { title = "Sneaky" }, outsider.Id), 403);
    }

    [Test]
    [Description("A caller who is not signed in is refused")]
    public async Task Anonymous_IsRefused()
    {
        ApiAssert.IsProblem(await Api.Get(Tasks), 401);
    }
}
