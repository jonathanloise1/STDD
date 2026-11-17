using Microsoft.AspNetCore.Mvc;
using MyApp.Monitor.Store;

namespace MyApp.Monitor.Dashboard.Pages;

public class ReviewModel(IIncidentStore store, IncidentActions actions) : DashboardPage
{
    public IReadOnlyList<IncidentRow> Incidents { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) =>
        Incidents = (await store.ListAsync(new IncidentFilter(Status: "closed"), 500, ct))
            .Where(i => Outcomes.NeedsRepositoryChange(i.Outcome))
            .OrderByDescending(i => i.ClosedAt)
            .ToList();

    public async Task<IActionResult> OnPostPullRequestAsync(string id, string? pullRequest, CancellationToken ct) =>
        Back("/review", await actions.SetPullRequestAsync(id, pullRequest, CurrentUser, ct));
}
