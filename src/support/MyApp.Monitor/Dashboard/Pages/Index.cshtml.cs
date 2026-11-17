using Microsoft.AspNetCore.Mvc;
using MyApp.Monitor.Store;

namespace MyApp.Monitor.Dashboard.Pages;

public class IndexModel(IIncidentStore store) : DashboardPage
{
    public static readonly string[] Triggers = ["severity", "rate", "regression", "noise_out_of_context", "noise_expired", "unknown", "health", "manual"];

    [BindProperty(SupportsGet = true, Name = "severity")] public string? Severity { get; set; }
    [BindProperty(SupportsGet = true, Name = "trigger")] public string? Trigger { get; set; }
    [BindProperty(SupportsGet = true, Name = "verdict")] public string? Verdict { get; set; }
    [BindProperty(SupportsGet = true, Name = "area")] public string? Area { get; set; }
    [BindProperty(SupportsGet = true, Name = "status")] public string? Status { get; set; }

    public IReadOnlyList<IncidentRow> Incidents { get; private set; } = [];
    public IReadOnlyDictionary<string, int> Counts { get; private set; } = new Dictionary<string, int>();
    public IReadOnlyList<string> Areas { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Counts = await store.CountByStatusAsync(ct);
        var all = await store.ListAsync(new IncidentFilter(
            Status: string.IsNullOrEmpty(Status) ? null : Status, Severity: Severity, Trigger: Trigger, VerdictSource: Verdict, Area: Area), 500, ct);
        Incidents = string.IsNullOrEmpty(Status) ? all.Where(i => i.Status != "closed").ToList() : all;
        Areas = (await store.ListAsync(new IncidentFilter(), 500, ct)).Select(i => i.Area).Where(a => !string.IsNullOrEmpty(a)).Select(a => a!).Distinct().Order().ToList();
    }
}
