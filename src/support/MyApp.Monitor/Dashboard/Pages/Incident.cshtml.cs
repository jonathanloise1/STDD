using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using MyApp.Monitor.Store;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Dashboard.Pages;

public class IncidentModel(IIncidentStore store, IncidentActions actions) : DashboardPage
{
    public IncidentRow? Row { get; private set; }
    public JsonObject Report { get; private set; } = new();
    public IReadOnlyList<IncidentEvent> Events { get; private set; } = [];
    public IReadOnlyList<NotificationRow> Notifications { get; private set; } = [];
    public IReadOnlyList<ToolAuditRow> Audit { get; private set; } = [];
    public InvestigationRow? Investigation { get; private set; }

    public static readonly IReadOnlyList<string> OutcomeValues = Outcomes.All;

    public async Task<IActionResult> OnGetAsync(string id, CancellationToken ct)
    {
        if (!await LoadAsync(id, ct)) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAcknowledgeAsync(string id, CancellationToken ct) =>
        Back($"/incidents/{id}", await actions.AcknowledgeAsync(id, CurrentUser, ct));

    public async Task<IActionResult> OnPostCloseAsync(
        string id, string value, string? ticket, string? pullRequest, string? note, string? duplicateOf, CancellationToken ct) =>
        Back($"/incidents/{id}", await actions.CloseAsync(id, new CloseRequest(value ?? "", ticket, pullRequest, note, duplicateOf), CurrentUser, ct));

    public async Task<IActionResult> OnPostSendDraftAsync(string id, long notificationId, CancellationToken ct)
    {
        var outcome = await actions.SendDraftAsync(notificationId, CurrentUser, ct);
        return Back($"/incidents/{id}", new ActionResult(outcome.Ok, outcome.Message));
    }

    public async Task<IActionResult> OnPostInvestigateAsync(string id, CancellationToken ct) =>
        Back($"/incidents/{id}", await actions.ReinvestigateAsync(id, CurrentUser, ct));

    private async Task<bool> LoadAsync(string id, CancellationToken ct)
    {
        Row = await store.GetAsync(id, ct);
        if (Row is null) return false;
        Report = Row.Report;
        Events = await store.EventsAsync(id, ct);
        Notifications = (await store.NotificationsAsync(id, null, 200, ct)).OrderBy(n => n.Id).ToList();
        Audit = (await store.ToolAuditAsync(id, 100, ct)).OrderBy(a => a.At).ToList();
        Investigation = await store.GetInvestigationAsync(id, ct);
        return true;
    }

    public string? Signature => MonitorJson.ScalarText(MonitorJson.Get(Report, "signature.template"));

    public JsonNode? Node(string path) => MonitorJson.Get(Report, path);
    public IEnumerable<JsonObject> Items(string path) => (Node(path) as JsonArray)?.OfType<JsonObject>() ?? [];
}
