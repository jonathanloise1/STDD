using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// Base class of the E2E tests of the audit area. A test that creates a task for the trail to record uses
/// <see cref="UniqueTitle"/>; after the test, the same user deletes the task through the Tasks page, so the
/// task list of the organization keeps the demo tasks in view from one run to the next (development.md 10.3).
/// Tests of the audit area never create data in "Empty Org".
/// </summary>
public abstract class AuditTestBase : AuthenticatedTestBase
{
    private readonly List<string> _titles = new();

    /// <summary>A title no other run has used, remembered for clean-up.</summary>
    protected string UniqueTitle(string text)
    {
        var title = Unique(text);
        _titles.Add(title);
        return title;
    }

    /// <summary>A title the test derives from a unique one (a renamed task), remembered for clean-up.</summary>
    protected string Keep(string title)
    {
        _titles.Add(title);
        return title;
    }

    protected override async Task CleanUpAsync()
    {
        if (_titles.Count == 0)
            return;

        try
        {
            var tasks = new TasksPage(Page);
            await tasks.Open();
            foreach (var title in _titles)
                await tasks.DeleteIfListed(title);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[E2E] Clean-up of created tasks failed: {ex.Message}");
        }
    }
}
