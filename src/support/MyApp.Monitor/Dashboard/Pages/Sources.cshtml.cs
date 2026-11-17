using MyApp.Monitor.Analysis;
using MyApp.Monitor.Hosting;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Store;

namespace MyApp.Monitor.Dashboard.Pages;

public class SourcesModel(IIncidentStore store, IRunContextProvider contexts, MonitorStatus status, Investigator.IInvestigator investigator) : DashboardPage
{
    public IReadOnlyList<SourceHealth> Health { get; private set; } = [];
    public IReadOnlyList<FileOffset> Offsets { get; private set; } = [];
    public IReadOnlyDictionary<string, string> Watermarks { get; private set; } = new Dictionary<string, string>();
    public IReadOnlyList<DiscoveredResource> Discovered { get; private set; } = [];
    public MonitorStatus Status => status;
    public RunContext Context => contexts.Current;
    public bool InvestigatorAvailable => investigator.IsAvailable;
    public Investigator.InvestigatorStatus InvestigatorStatus => investigator.Status;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Health = await status.HealthAsync(ct);
        Offsets = await store.AllOffsetsAsync(ct);
        Watermarks = await store.AllWatermarksAsync(ct);
        Discovered = await store.AllDiscoveredAsync(ct);
    }
}
