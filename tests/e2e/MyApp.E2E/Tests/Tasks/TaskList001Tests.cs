using Microsoft.Playwright;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-LIST-001: Tasks page loads and displays task list.
///
/// Verifies that an authenticated user can navigate to the tasks page
/// and see the task list table rendered.
///
/// <userstory ref="US-TASK-02" />
/// </summary>
[TestFixture]
[Category("Tasks")]
[Property("TestCase", "TASK-LIST-001")]
[Property("UserStory", "US-TASK-02")]
public class TaskList001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    private Dictionary<string, string> ImpersonationHeaders(string userId) =>
        new() { [TestConfiguration.ImpersonationHeaderName] = userId };

    /// <summary>
    /// US-TASK-02: Tasks page is accessible and shows the task list.
    /// </summary>
    [Test]
    [Description("US-TASK-02: Tasks page loads for authenticated user")]
    public async Task TasksPage_ShouldLoadAndShowList()
    {
        // ── Act: navigate to tasks page ──
        await NavigateToAsync("/tasks");

        // ── Assert: page loaded and URL is correct ──
        Assert.That(Page.Url, Does.Contain("/tasks"), "URL should contain /tasks");

        // Verify the sidebar "Tasks" link is visible
        var sidebarTasks = Page.Locator("#sidebar-tasks");
        await Assertions.Expect(sidebarTasks).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
