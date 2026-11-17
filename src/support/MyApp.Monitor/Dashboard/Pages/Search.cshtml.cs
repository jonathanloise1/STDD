using Microsoft.AspNetCore.Mvc;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Dashboard.Pages;

public class SearchModel(IncidentActions actions, IRunContextProvider contexts) : DashboardPage
{
    [BindProperty(SupportsGet = true, Name = "trace")] public string? Trace { get; set; }
    [BindProperty(SupportsGet = true, Name = "entity")] public string? Entity { get; set; }
    [BindProperty(SupportsGet = true, Name = "id")] public string? EntityId { get; set; }

    public ToolEnvelope? Result { get; private set; }
    public string? Searched { get; private set; }
    public IReadOnlyCollection<string> Entities => contexts.Current.Pack.Entities.Names;

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(Trace))
        {
            Searched = "trace " + Trace.Trim();
            Result = await actions.SearchTraceAsync(CurrentUser, Trace, ct);
        }
        else if (!string.IsNullOrWhiteSpace(Entity) && !string.IsNullOrWhiteSpace(EntityId))
        {
            Searched = $"entity {Entity.Trim()} {EntityId.Trim()}";
            Result = await actions.SearchEntityAsync(CurrentUser, Entity, EntityId, ct);
        }
    }

    public async Task<IActionResult> OnPostOpenIncidentAsync(string? trace, string? entity, string? entityId, string? note, CancellationToken ct)
    {
        var hasTrace = !string.IsNullOrWhiteSpace(trace);
        var hasEntity = !string.IsNullOrWhiteSpace(entity) && !string.IsNullOrWhiteSpace(entityId);
        if (!hasTrace && !hasEntity) return Back("/search", new ActionResult(false, "Search for a trace id or an entity first."));
        var row = await actions.OpenManualAsync(new ManualIncidentRequest(
            CurrentUser, hasTrace ? trace!.Trim().ToLowerInvariant() : null, hasEntity ? entity!.Trim() : null, hasEntity ? entityId!.Trim() : null, note), ct);
        return Back($"/incidents/{row.Id}", new ActionResult(true, "Incident opened manually."));
    }
}
