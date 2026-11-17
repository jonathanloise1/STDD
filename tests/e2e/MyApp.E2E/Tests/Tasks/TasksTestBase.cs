using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// Base class of the E2E tests of the tasks area. Tests create tasks with titles from
/// <see cref="UniqueTitle"/>; after the test, the same user deletes them through the Tasks page, so the
/// list of the organization (which shows only the 50 newest tasks) keeps the demo tasks of the seed data in
/// view from one run to the next (development.md 10.3: restore the state through the UI).
/// </summary>
public abstract class TasksTestBase : AuthenticatedTestBase
{
    private readonly List<string> _titles = new();

    /// <summary>
    /// A title no other run has used, remembered for clean-up. Use it for every task the test creates.
    /// </summary>
    protected string UniqueTitle(string text)
    {
        var title = Unique(text);
        _titles.Add(title);
        return title;
    }

    protected override async Task CleanUpAsync()
    {
        if (_titles.Count == 0)
            return;

        try
        {
            ServiceAnswersAgain();
            var tasks = new TasksPage(Page);
            await tasks.Open();
            foreach (var title in _titles)
                await tasks.DeleteIfListed(title);
        }
        catch (Exception ex)
        {
            // Clean-up is best effort: a failed test may leave the page in a state the clean-up cannot use
            Console.WriteLine($"[E2E] Clean-up of created tasks failed: {ex.Message}");
        }
    }
}
