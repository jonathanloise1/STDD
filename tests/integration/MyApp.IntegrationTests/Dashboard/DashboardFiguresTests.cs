using MyApp.Domain.Constants;
using MyApp.Infrastructure.Seed;
using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Dashboard;

/// <summary>
/// The figures of the dashboard: the task counts and the five newest tasks. Replaces the old DashKpi001Tests,
/// which only checked that the page loaded after creating an organization. "Stats Org" holds the seeded
/// tasks the counts are checked against; new organizations cover the empty and the larger cases.
/// </summary>
[TestFixture]
[Category("dashboard")]
[Property("UserStory", "US-DASH-01")]
[Property("UserStory", "US-DASH-02")]
public class DashboardFiguresTests
{
    private static List<SeedTask> StatsTasks() =>
        SeedDataSet.Tasks(DateTime.UtcNow).Where(t => t.OrganizationId == SeedDataSet.StatsOrg).ToList();

    [Test]
    [Description("The counts of Stats Org match its seeded tasks, by status and in total")]
    public async Task Counts_MatchTheSeededTasks()
    {
        var tasks = StatsTasks();

        var response = await Api.Get($"/api/organizations/{SeedDataSet.StatsOrg}/dashboard/counts", SeedDataSet.StatsAdmin);

        ApiAssert.IsSuccess(response);
        Assert.That(response.Json.GetProperty("total").GetInt32(), Is.EqualTo(tasks.Count));
        Assert.That(response.Json.GetProperty("todo").GetInt32(), Is.EqualTo(tasks.Count(t => t.Status == TaskItemStatus.Todo)));
        Assert.That(response.Json.GetProperty("inProgress").GetInt32(), Is.EqualTo(tasks.Count(t => t.Status == TaskItemStatus.InProgress)));
        Assert.That(response.Json.GetProperty("done").GetInt32(), Is.EqualTo(tasks.Count(t => t.Status == TaskItemStatus.Done)));
        Assert.That(response.Json.GetProperty("cancelled").GetInt32(), Is.EqualTo(tasks.Count(t => t.Status == TaskItemStatus.Cancelled)));
    }

    [Test]
    [Description("A Viewer sees the same counts as an Admin")]
    public async Task Counts_AreTheSameForAViewer()
    {
        var asAdmin = await Api.Get($"/api/organizations/{SeedDataSet.StatsOrg}/dashboard/counts", SeedDataSet.StatsAdmin);
        var asViewer = await Api.Get($"/api/organizations/{SeedDataSet.StatsOrg}/dashboard/counts", SeedDataSet.StatsViewer);

        ApiAssert.IsSuccess(asViewer);
        Assert.That(asViewer.Body, Is.EqualTo(asAdmin.Body));
    }

    [Test]
    [Description("An organization without tasks has every count at zero")]
    public async Task Counts_OfAnEmptyOrganization_AreZero()
    {
        var response = await Api.Get($"/api/organizations/{SeedDataSet.EmptyOrg}/dashboard/counts", SeedDataSet.EmptyAdmin);

        ApiAssert.IsSuccess(response);
        foreach (var field in new[] { "total", "todo", "inProgress", "done", "cancelled" })
            Assert.That(response.Json.GetProperty(field).GetInt32(), Is.EqualTo(0), field);
    }

    [Test]
    [Description("The counts of an organization the user does not belong to are refused")]
    public async Task Counts_AreRefusedToANonMember()
    {
        var response = await Api.Get($"/api/organizations/{SeedDataSet.StatsOrg}/dashboard/counts", SeedDataSet.EmptyAdmin);

        ApiAssert.IsProblem(response, 403);
    }

    [Test]
    [Description("The latest tasks are the five newest of Stats Org, newest first")]
    public async Task RecentTasks_AreTheFiveNewest()
    {
        var expected = StatsTasks().OrderByDescending(t => t.CreatedAt).Take(5).Select(t => t.Title).ToList();

        var response = await Api.Get($"/api/organizations/{SeedDataSet.StatsOrg}/dashboard/recent-tasks", SeedDataSet.StatsAdmin);

        ApiAssert.IsSuccess(response);
        var titles = response.Json.EnumerateArray().Select(t => t.GetProperty("title").GetString()).ToList();
        Assert.That(titles, Is.EqualTo(expected));
    }

    [Test]
    [Description("With more than five tasks only the five newest are returned")]
    public async Task RecentTasks_AreLimitedToFive()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        for (var i = 1; i <= 6; i++)
        {
            await TestWorld.CreateTask(organizationId, admin, $"Task {i}");
            await Task.Delay(30);
        }

        var response = await Api.Get($"/api/organizations/{organizationId}/dashboard/recent-tasks", admin.Id);

        var titles = response.Json.EnumerateArray().Select(t => t.GetProperty("title").GetString()).ToList();
        Assert.That(titles, Is.EqualTo(new[] { "Task 6", "Task 5", "Task 4", "Task 3", "Task 2" }));
    }

    [Test]
    [Description("An organization without tasks has no latest tasks")]
    public async Task RecentTasks_OfAnEmptyOrganization_AreEmpty()
    {
        var response = await Api.Get($"/api/organizations/{SeedDataSet.EmptyOrg}/dashboard/recent-tasks", SeedDataSet.EmptyAdmin);

        ApiAssert.IsSuccess(response);
        Assert.That(response.Json.GetArrayLength(), Is.EqualTo(0));
    }
}
