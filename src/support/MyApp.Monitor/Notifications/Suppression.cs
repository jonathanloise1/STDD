using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyApp.Monitor.Store;
using static MyApp.Monitor.Store.SqliteDatabase;

namespace MyApp.Monitor.Notifications;

/// <summary>A suppression window (operations.md 10.4): fixed (<c>starts_at</c>, <c>ends_at</c>) or weekly (<c>day</c>, <c>from</c>, <c>to</c>, local time).</summary>
public sealed record SuppressionWindow(
    string Name, string Match, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, DayOfWeek? Day, TimeOnly? From, TimeOnly? To, string Origin)
{
    public bool Matches(string signatureOrTemplateId) => Glob(Match).IsMatch(signatureOrTemplateId);

    private static readonly Dictionary<string, Regex> GlobCache = new(StringComparer.Ordinal);

    private static Regex Glob(string pattern)
    {
        lock (GlobCache)
        {
            if (!GlobCache.TryGetValue(pattern, out var regex))
                GlobCache[pattern] = regex = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            return regex;
        }
    }

    /// <summary>The instance of the window that contains <paramref name="now"/>, or null when the window is not active.</summary>
    public (DateTimeOffset Start, DateTimeOffset End)? ActiveInstance(DateTimeOffset now, TimeZoneInfo zone)
    {
        if (StartsAt is not null && EndsAt is not null)
            return now >= StartsAt && now < EndsAt ? (StartsAt.Value, EndsAt.Value) : null;
        if (Day is null || From is null || To is null) return null;

        var local = TimeZoneInfo.ConvertTime(now, zone);
        var duration = To.Value - From.Value;   // TimeOnly subtraction wraps past midnight
        if (duration == TimeSpan.Zero) duration = TimeSpan.FromHours(24);
        // the instance can have started today or, across midnight, yesterday
        foreach (var back in new[] { 0, 1 })
        {
            var date = DateOnly.FromDateTime(local.Date).AddDays(-back);
            if (date.DayOfWeek != Day) continue;
            var startLocal = date.ToDateTime(From.Value, DateTimeKind.Unspecified);
            var start = new DateTimeOffset(startLocal, zone.GetUtcOffset(startLocal));
            var end = start + duration;
            if (now >= start && now < end) return (start, end);
        }
        return null;
    }

    public static IReadOnlyList<SuppressionWindow> FromProfile(JsonNode? suppression)
    {
        var windows = new List<SuppressionWindow>();
        foreach (var node in (suppression as JsonArray) ?? [])
        {
            if (node is not JsonObject o) continue;
            var name = MonitorJson.ScalarText(o["name"]) ?? "window";
            var match = MonitorJson.ScalarText(o["match"]) ?? "*";
            if (o["weekly"] is JsonObject w
                && Enum.TryParse<DayOfWeek>(MonitorJson.ScalarText(w["day"]), ignoreCase: true, out var day)
                && TimeOnly.TryParse(MonitorJson.ScalarText(w["from"]), CultureInfo.InvariantCulture, out var from)
                && TimeOnly.TryParse(MonitorJson.ScalarText(w["to"]), CultureInfo.InvariantCulture, out var to))
                windows.Add(new SuppressionWindow(name, match, null, null, day, from, to, "profile"));
            else if (DateTimeOffset.TryParse(MonitorJson.ScalarText(o["starts_at"]), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var s)
                     && DateTimeOffset.TryParse(MonitorJson.ScalarText(o["ends_at"]), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var e))
                windows.Add(new SuppressionWindow(name, match, s, e, null, null, null, "profile"));
        }
        return windows;
    }
}

/// <summary>
/// Suppression (operations.md 10.4): inside a window, records that would escalate update counters only (the analyser already counted
/// them); when the window instance ends, one <c>suppression_summary</c> event lists the counts. Windows come from the profile or
/// from the dashboard (table <c>suppression_windows</c>).
/// </summary>
public sealed class SuppressionService(IIncidentStore store, TimeProvider time)
{
    public const string SummaryKind = "suppression_summary";
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(5);

    private readonly object _lock = new();
    private IReadOnlyList<SuppressionWindow> _profile = [];
    private IReadOnlyList<SuppressionWindow> _stored = [];
    private long _storedAt = long.MinValue;
    private readonly Dictionary<(string Name, DateTimeOffset Start, DateTimeOffset End), SortedDictionary<string, int>> _hits = [];

    public void Configure(JsonNode? profileWindows)
    {
        lock (_lock) _profile = SuppressionWindow.FromProfile(profileWindows);
    }

    /// <summary>Forgets the cached dashboard windows so that a window just added applies at once.</summary>
    public void Invalidate()
    {
        lock (_lock) _storedAt = long.MinValue;
    }

    private async Task<IReadOnlyList<SuppressionWindow>> AllAsync(CancellationToken ct)
    {
        bool stale;
        lock (_lock) stale = _storedAt == long.MinValue || Stopwatch.GetElapsedTime(_storedAt) > CacheFor;
        if (stale)
        {
            var rows = await store.SuppressionWindowsAsync(ct).ConfigureAwait(false);
            var windows = rows.Select(r => new SuppressionWindow(r.Name, r.Match, r.StartsAt, r.EndsAt, null, null, null, "dashboard")).ToList();
            lock (_lock) { _stored = windows; _storedAt = Stopwatch.GetTimestamp(); }
        }
        lock (_lock) return _profile.Concat(_stored).ToList();
    }

    /// <summary>The name of the window that suppresses this signature or template now, or null. A hit is counted for the summary.</summary>
    public async Task<string?> CheckAsync(string signatureOrTemplateId, DateTimeOffset now, CancellationToken ct = default)
    {
        var zone = time.LocalTimeZone;
        foreach (var window in await AllAsync(ct).ConfigureAwait(false))
        {
            if (!window.Matches(signatureOrTemplateId) || window.ActiveInstance(now, zone) is not { } instance) continue;
            lock (_lock)
            {
                var key = (window.Name, instance.Start, instance.End);
                if (!_hits.TryGetValue(key, out var counts)) _hits[key] = counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
                counts[signatureOrTemplateId] = counts.GetValueOrDefault(signatureOrTemplateId) + 1;
            }
            return window.Name;
        }
        return null;
    }

    /// <summary>Writes the summary of every window instance that has ended. Called on a timer and at shutdown (with <paramref name="all"/>).</summary>
    public async Task FlushEndedAsync(DateTimeOffset now, CancellationToken ct = default, bool all = false)
    {
        List<((string Name, DateTimeOffset Start, DateTimeOffset End) Key, SortedDictionary<string, int> Counts)> ended;
        lock (_lock)
        {
            ended = _hits.Where(kv => all || kv.Key.End <= now).Select(kv => (kv.Key, kv.Value)).ToList();
            foreach (var (key, _) in ended) _hits.Remove(key);
        }
        foreach (var (key, counts) in ended)
        {
            var data = new JsonObject
            {
                ["window"] = key.Name, ["starts_at"] = Iso(key.Start), ["ends_at"] = Iso(key.End),
                ["total"] = counts.Values.Sum(),
                ["counts"] = new JsonObject(counts.Select(kv => KeyValuePair.Create(kv.Key, (JsonNode?)JsonValue.Create(kv.Value)))),
            };
            await store.AddEventAsync("window:" + key.Name, now, SummaryKind, data, ct).ConfigureAwait(false);
        }
    }
}
