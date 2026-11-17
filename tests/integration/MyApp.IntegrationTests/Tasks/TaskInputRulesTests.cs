using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Tasks;

/// <summary>
/// What a task may hold: a title that is not empty and at most 200 characters, a description of at most 2000.
/// The browser stops typing at these limits; the backend refuses a request that goes past them.
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("UserStory", "US-TASK-01")]
[Property("UserStory", "US-TASK-02")]
public class TaskInputRulesTests
{
    private Guid _organizationId;
    private Person _admin = null!;

    [SetUp]
    public async Task NewOrganization() => (_organizationId, _admin) = await TestWorld.CreateOrganization();

    private string Tasks => TestWorld.TasksPath(_organizationId);

    [TestCase("")]
    [TestCase("   ")]
    [Description("An empty title, or one made only of spaces, is refused with a message on the title")]
    public async Task Create_WithAnEmptyTitle_IsRefused(string title)
    {
        var response = await Api.Post(Tasks, new { title }, _admin.Id);

        ApiAssert.IsProblem(response, 400, detail: "Enter a title.");
        Assert.That(response.Problem.Errors!["title"], Does.Contain("Enter a title."));
    }

    [Test]
    [Description("A title of 201 characters is refused, one of 200 is accepted")]
    public async Task Create_TitleLimit_Is200()
    {
        var refused = await Api.Post(Tasks, new { title = new string('a', 201) }, _admin.Id);
        var accepted = await Api.Post(Tasks, new { title = new string('b', 200) }, _admin.Id);

        ApiAssert.IsProblem(refused, 400, detail: "The title can have at most 200 characters.");
        ApiAssert.IsSuccess(accepted, 201);
    }

    [Test]
    [Description("A description of 2001 characters is refused")]
    public async Task Create_DescriptionLimit_Is2000()
    {
        var refused = await Api.Post(Tasks, new { title = "Long description", description = new string('d', 2001) }, _admin.Id);

        ApiAssert.IsProblem(refused, 400, detail: "The description can have at most 2000 characters.");
    }

    [Test]
    [Description("The title is stored without the spaces around it")]
    public async Task Create_TrimsTheTitle()
    {
        var response = await Api.Post(Tasks, new { title = "  Padded title  " }, _admin.Id);

        ApiAssert.IsSuccess(response, 201);
        Assert.That(response.String("title"), Is.EqualTo("Padded title"));
    }

    [Test]
    [Description("Updating with an empty title is refused and the task keeps its title")]
    public async Task Update_WithAnEmptyTitle_IsRefused()
    {
        var id = await TestWorld.CreateTask(_organizationId, _admin, "Keep this title");

        var response = await Api.Put($"{Tasks}/{id}", new { title = " " }, _admin.Id);

        ApiAssert.IsProblem(response, 400, detail: "Enter a title.");
        var stored = await Api.Get($"{Tasks}/{id}", _admin.Id);
        Assert.That(stored.String("title"), Is.EqualTo("Keep this title"));
    }
}
