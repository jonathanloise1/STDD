using System.Text.Json;
using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-CRUD-001: Full CRUD lifecycle for task items via API.
///
/// Creates a task, reads it, updates it, and deletes it — all via
/// the backend API with impersonation headers.
///
/// <userstory ref="US-TASK-01, US-TASK-02, US-TASK-03" />
/// </summary>
[TestFixture]
[Category("Tasks")]
[Property("TestCase", "TASK-CRUD-001")]
[Property("UserStory", "US-TASK-01")]
public class TaskCrud001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    private string? _createdTaskId;

    private string ApiUrl(string path = "") =>
        $"{TestConfiguration.ApiBaseUrl}/api/organizations/{TestUsers.OrgDevId}/tasks{path}";

    private Dictionary<string, string> ImpersonationHeaders(string userId) =>
        new() { [TestConfiguration.ImpersonationHeaderName] = userId };

    [TearDown]
    public async Task CleanupTask()
    {
        if (_createdTaskId == null) return;
        try
        {
            await Page.APIRequest.DeleteAsync(
                ApiUrl($"/{_createdTaskId}"),
                new()
                {
                    Headers = ImpersonationHeaders(TestUsers.AdminUser.Id),
                    IgnoreHTTPSErrors = true
                });
        }
        catch
        {
            // Ignore cleanup failures
        }
        _createdTaskId = null;
    }

    /// <summary>
    /// US-TASK-01: Create a task via API.
    /// </summary>
    [Test]
    [Order(1)]
    [Description("US-TASK-01: Create a task via API returns 201")]
    public async Task CreateTask_ShouldReturn201()
    {
        // ── Arrange ──
        var taskTitle = UniqueTestName("Task");

        // ── Act ──
        var response = await Page.APIRequest.PostAsync(
            ApiUrl(),
            new()
            {
                Headers = ImpersonationHeaders(TestUsers.AdminUser.Id),
                DataObject = new
                {
                    title = taskTitle,
                    description = "E2E test task description"
                },
                IgnoreHTTPSErrors = true
            });

        // ── Assert ──
        Assert.That(response.Status, Is.EqualTo(201), "POST /tasks should return 201 Created");

        var json = await response.JsonAsync();
        _createdTaskId = json?.GetProperty("id").GetString();
        Assert.That(_createdTaskId, Is.Not.Null.And.Not.Empty, "Response should contain task ID");

        var returnedTitle = json?.GetProperty("title").GetString();
        Assert.That(returnedTitle, Is.EqualTo(taskTitle));

        var returnedStatus = json?.GetProperty("status").GetString();
        Assert.That(returnedStatus, Is.EqualTo("Todo"), "New task should have status 'Todo'");
    }

    /// <summary>
    /// US-TASK-02: List tasks includes the created task.
    /// </summary>
    [Test]
    [Order(2)]
    [Description("US-TASK-02: Listing tasks includes the created task")]
    public async Task ListTasks_ShouldIncludeCreatedTask()
    {
        // ── Arrange: create a task first ──
        var taskTitle = UniqueTestName("ListTask");
        var createResponse = await Page.APIRequest.PostAsync(
            ApiUrl(),
            new()
            {
                Headers = ImpersonationHeaders(TestUsers.AdminUser.Id),
                DataObject = new { title = taskTitle },
                IgnoreHTTPSErrors = true
            });
        Assert.That(createResponse.Status, Is.EqualTo(201));
        var createJson = await createResponse.JsonAsync();
        _createdTaskId = createJson?.GetProperty("id").GetString();

        // ── Act: list tasks ──
        var response = await Page.APIRequest.GetAsync(
            ApiUrl($"?search={Uri.EscapeDataString(taskTitle)}"),
            new()
            {
                Headers = ImpersonationHeaders(TestUsers.AdminUser.Id),
                IgnoreHTTPSErrors = true
            });

        // ── Assert ──
        Assert.That(response.Status, Is.EqualTo(200));
        var json = await response.JsonAsync();
        var totalCount = json?.GetProperty("totalCount").GetInt32();
        Assert.That(totalCount, Is.GreaterThanOrEqualTo(1), "Should find at least 1 task");

        var items = json?.GetProperty("items");
        var found = false;
        for (int i = 0; i < items?.GetArrayLength(); i++)
        {
            if (items.Value[i].GetProperty("title").GetString() == taskTitle)
            {
                found = true;
                break;
            }
        }
        Assert.That(found, Is.True, $"Task '{taskTitle}' should appear in the list");
    }

    /// <summary>
    /// US-TASK-03: Update a task via API.
    /// </summary>
    [Test]
    [Order(3)]
    [Description("US-TASK-03: Update a task changes its status")]
    public async Task UpdateTask_ShouldChangeStatus()
    {
        // ── Arrange: create a task ──
        var taskTitle = UniqueTestName("UpdateTask");
        var createResponse = await Page.APIRequest.PostAsync(
            ApiUrl(),
            new()
            {
                Headers = ImpersonationHeaders(TestUsers.AdminUser.Id),
                DataObject = new { title = taskTitle },
                IgnoreHTTPSErrors = true
            });
        Assert.That(createResponse.Status, Is.EqualTo(201));
        var createJson = await createResponse.JsonAsync();
        _createdTaskId = createJson?.GetProperty("id").GetString();

        // ── Act: update status to InProgress ──
        var updateResponse = await Page.APIRequest.PutAsync(
            ApiUrl($"/{_createdTaskId}"),
            new()
            {
                Headers = ImpersonationHeaders(TestUsers.AdminUser.Id),
                DataObject = new
                {
                    title = taskTitle,
                    description = "Updated description",
                    status = 1 // InProgress
                },
                IgnoreHTTPSErrors = true
            });

        // ── Assert ──
        Assert.That(updateResponse.Status, Is.EqualTo(200), "PUT should return 200");
        var json = await updateResponse.JsonAsync();
        var updatedStatus = json?.GetProperty("status").GetString();
        Assert.That(updatedStatus, Is.EqualTo("InProgress"), "Status should be updated to InProgress");
    }

    /// <summary>
    /// US-TASK-03: Delete a task via API.
    /// </summary>
    [Test]
    [Order(4)]
    [Description("US-TASK-03: Delete a task returns 204 and task is gone")]
    public async Task DeleteTask_ShouldReturn204()
    {
        // ── Arrange: create a task ──
        var taskTitle = UniqueTestName("DeleteTask");
        var createResponse = await Page.APIRequest.PostAsync(
            ApiUrl(),
            new()
            {
                Headers = ImpersonationHeaders(TestUsers.AdminUser.Id),
                DataObject = new { title = taskTitle },
                IgnoreHTTPSErrors = true
            });
        Assert.That(createResponse.Status, Is.EqualTo(201));
        var createJson = await createResponse.JsonAsync();
        var taskId = createJson?.GetProperty("id").GetString();

        // ── Act: delete ──
        var deleteResponse = await Page.APIRequest.DeleteAsync(
            ApiUrl($"/{taskId}"),
            new()
            {
                Headers = ImpersonationHeaders(TestUsers.AdminUser.Id),
                IgnoreHTTPSErrors = true
            });

        // ── Assert: 204 No Content ──
        Assert.That(deleteResponse.Status, Is.EqualTo(204), "DELETE should return 204");

        // ── Assert: GET returns 404 ──
        var getResponse = await Page.APIRequest.GetAsync(
            ApiUrl($"/{taskId}"),
            new()
            {
                Headers = ImpersonationHeaders(TestUsers.AdminUser.Id),
                IgnoreHTTPSErrors = true
            });
        Assert.That(getResponse.Status, Is.EqualTo(404), "Deleted task should return 404");

        // Task was already deleted, no cleanup needed
        _createdTaskId = null;
    }
}
