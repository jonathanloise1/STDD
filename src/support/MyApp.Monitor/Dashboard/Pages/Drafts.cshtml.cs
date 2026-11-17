using Microsoft.AspNetCore.Mvc;
using MyApp.Monitor.Store;

namespace MyApp.Monitor.Dashboard.Pages;

public class DraftsModel(IIncidentStore store, IncidentActions actions) : DashboardPage
{
    public IReadOnlyList<NotificationRow> Drafts { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct) =>
        Drafts = (await store.NotificationsAsync(kind: MyApp.Monitor.Notifications.NotificationKinds.Draft, limit: 200, ct: ct)).OrderBy(n => n.Id).ToList();

    public async Task<IActionResult> OnPostSendAsync(long notificationId, CancellationToken ct)
    {
        var outcome = await actions.SendDraftAsync(notificationId, CurrentUser, ct);
        return Back("/drafts", new ActionResult(outcome.Ok, outcome.Message));
    }
}
