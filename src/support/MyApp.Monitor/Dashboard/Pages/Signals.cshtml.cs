using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Store;

namespace MyApp.Monitor.Dashboard.Pages;

public sealed record NoiseRow(string Id, string Rationale, string Expires, bool Expired, int Total, int LastHour);

public class SignalsModel(IIncidentStore store, IUnknownTemplateSink unknowns, IRunContextProvider contexts, TimeProvider time, IncidentActions actions) : DashboardPage
{
    public IReadOnlyList<CounterTotal> Counters { get; private set; } = [];
    public IReadOnlyDictionary<string, Signature> Signatures { get; private set; } = new Dictionary<string, Signature>();
    public IReadOnlyList<NoiseRow> Noise { get; private set; } = [];
    public IReadOnlyList<UnknownTemplate> Unknown { get; private set; } = [];
    public IReadOnlyList<SuppressionWindow> Windows { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var ctx = contexts.Current;
        Counters = await store.CounterTotalsAsync(now, ct);
        Signatures = ctx.Pack.Signatures.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        Noise = ctx.Pack.Signatures.Where(s => s.Status == SignatureStatus.Noise).OrderBy(s => s.Id, StringComparer.Ordinal).Select(s =>
        {
            var rows = Counters.Where(c => c.SignatureOrTemplate == s.Id).ToList();
            var expires = s.Noise?.Expires ?? "";
            var expired = !DateOnly.TryParseExact(expires, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date < today;
            return new NoiseRow(s.Id, s.Noise?.Rationale ?? "", expires, expired, rows.Sum(c => c.Total), rows.Sum(c => c.LastHour));
        }).ToList();
        Unknown = unknowns.All().OrderByDescending(u => u.LastSeen).ToList();
        var stored = await store.SuppressionWindowsAsync(ct);
        Windows = SuppressionWindow.FromProfile(ctx.Profile.Suppression)
            .Concat(stored.Select(r => new SuppressionWindow(r.Name, r.Match, r.StartsAt, r.EndsAt, null, null, null, "dashboard")))
            .ToList();
    }

    public async Task<IActionResult> OnPostAddWindowAsync(string name, string startsAt, string endsAt, string match, string? reason, CancellationToken ct)
    {
        const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (!DateTimeOffset.TryParse(startsAt, CultureInfo.InvariantCulture, styles, out var from) || !DateTimeOffset.TryParse(endsAt, CultureInfo.InvariantCulture, styles, out var to))
            return Back("/signals", new ActionResult(false, "Give the start and the end as UTC date and time, for example 2026-11-07 08:00."));
        return Back("/signals", await actions.AddWindowAsync(name ?? "", from, to, match ?? "", reason, CurrentUser, ct));
    }
}
