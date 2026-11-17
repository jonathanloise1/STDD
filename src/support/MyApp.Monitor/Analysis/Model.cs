using System.Globalization;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;

namespace MyApp.Monitor.Analysis;

public static class Triggers
{
    public const string Severity = "severity";
    public const string Rate = "rate";
    public const string Regression = "regression";
    public const string NoiseOutOfContext = "noise_out_of_context";
    public const string NoiseExpired = "noise_expired";
    public const string Unknown = "unknown";
    public const string Health = "health";
    public const string Manual = "manual";
}

public static class Severities
{
    public static readonly string[] Ordered = ["Information", "Warning", "Error", "Critical"];

    public static int Rank(string severity) => Math.Max(0, Array.IndexOf(Ordered, severity));

    public static string Raise(string severity) => Ordered[Math.Min(Rank(severity) + 1, Ordered.Length - 1)];

    public static string AtLeast(string severity, string minimum) => Rank(severity) >= Rank(minimum) ? severity : minimum;

    public static string FromLevel(RecordLevel level) => level switch
    {
        RecordLevel.Critical => "Critical",
        RecordLevel.Error => "Error",
        RecordLevel.Warning => "Warning",
        _ => "Information",
    };
}

/// <summary>Semantic version comparison for <c>fixed_in</c> (regression trigger).</summary>
public static class SemanticVersion
{
    public static bool TryParse(string? text, out (int Major, int Minor, int Patch, bool Prerelease) version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim().TrimStart('v');
        var plus = t.IndexOf('+');
        if (plus >= 0) t = t[..plus];
        var pre = false;
        var dash = t.IndexOf('-');
        if (dash >= 0) { pre = true; t = t[..dash]; }
        var parts = t.Split('.');
        if (parts.Length is < 2 or > 3) return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)) return false;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)) return false;
        var patch = 0;
        if (parts.Length == 3 && !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out patch)) return false;
        version = (major, minor, patch, pre);
        return true;
    }

    /// <summary>True when <paramref name="actual"/> is at least <paramref name="fixedIn"/>. A missing or unreadable version counts as newer.</summary>
    public static bool IsAtLeast(string? actual, string fixedIn)
    {
        if (!TryParse(actual, out var a) || !TryParse(fixedIn, out var f)) return true;
        var cmp = a.Major != f.Major ? a.Major.CompareTo(f.Major)
            : a.Minor != f.Minor ? a.Minor.CompareTo(f.Minor)
            : a.Patch != f.Patch ? a.Patch.CompareTo(f.Patch)
            : (a.Prerelease == f.Prerelease ? 0 : a.Prerelease ? -1 : 1);
        return cmp >= 0;
    }
}

public enum MatchKind { Matched, UnknownKey, UnknownTemplate }

/// <summary>Result of matching (operations.md 6.6). <see cref="Id"/> is the signature id, the unknown key or the template id.</summary>
public sealed record MatchResult(MatchKind Kind, Signature? Signature, string Id, TemplateInfo? Template);

public interface ISignatureCatalog
{
    Signature? ByLogKey(string key);
    Signature? ByTemplateId(string templateId);
}

public sealed class PackSignatureCatalog : ISignatureCatalog
{
    private readonly Dictionary<string, Signature> _keyed;
    private readonly Dictionary<string, Signature> _template;

    public PackSignatureCatalog(IEnumerable<Signature> signatures)
    {
        var all = signatures.ToList();
        _keyed = all.Where(s => s.IsKeyed).ToDictionary(s => s.Id, StringComparer.Ordinal);
        _template = all.Where(s => !s.IsKeyed).ToDictionary(s => s.Id, StringComparer.Ordinal);
    }

    public Signature? ByLogKey(string key) => _keyed.GetValueOrDefault(key);
    public Signature? ByTemplateId(string templateId) => _template.GetValueOrDefault(templateId);
}

public enum EscalationAction { OpenNew, AppendToOpen, RaiseSeverity }

/// <summary>An incident that is open or acknowledged, or the last one closed, for a fingerprint.</summary>
public sealed record IncidentRef(string Id, string Fingerprint, string Status, string Severity, string? Outcome);

/// <summary>
/// An escalation decision (operations.md 6.7, 6.8): the data the next pass turns into an incident. <see cref="Residual"/>
/// says the verdict is left to the investigator; <see cref="Action"/> says whether it opens an incident or folds into one.
/// </summary>
public sealed record Escalation(
    LogRecord Record,
    MatchResult Match,
    string Trigger,
    string Severity,
    bool Residual,
    string Fingerprint,
    string SignatureOrTemplateId,
    int FingerprintWindowCount,
    double FingerprintRatePerHour,
    int SignatureWindowCount,
    double SignatureRatePerHour,
    EscalationAction Action,
    IncidentRef? Open,
    string? PreviousIncidentId)
{
    public Signature? Signature => Match.Signature;
}

public sealed record AnalysisResult(
    LogRecord Record, TemplateInfo? Template, MatchResult Match, string Fingerprint, int SignatureWindowCount, double SignatureRatePerHour,
    Escalation? Escalation, string? CountedOnlyReason)
{
    public bool Escalates => Escalation is not null;
}

public sealed record CounterRow(string SignatureOrTemplate, string Environment, string Fingerprint, DateTimeOffset BucketStart, int Count);

/// <summary>Hourly counters per signature or template id, environment and fingerprint (operations.md 6.7, table <c>counters</c>).</summary>
public interface ICounterStore
{
    void Increment(string signatureOrTemplate, string environment, string fingerprint, DateTimeOffset bucketStart, int count);
    IReadOnlyList<CounterRow> Snapshot();
}

public sealed class InMemoryCounterStore : ICounterStore
{
    private readonly object _lock = new();
    private readonly Dictionary<(string, string, string, DateTimeOffset), int> _rows = [];

    public void Increment(string signatureOrTemplate, string environment, string fingerprint, DateTimeOffset bucketStart, int count)
    {
        lock (_lock)
        {
            var key = (signatureOrTemplate, environment, fingerprint, bucketStart);
            _rows[key] = _rows.GetValueOrDefault(key) + count;
        }
    }

    public IReadOnlyList<CounterRow> Snapshot()
    {
        lock (_lock) return _rows.Select(kv => new CounterRow(kv.Key.Item1, kv.Key.Item2, kv.Key.Item3, kv.Key.Item4, kv.Value)).ToList();
    }
}

public sealed record UnknownTemplate(
    string TemplateId, string? EventName, string Category, string? ExceptionType, string? TopFrame, string Template, string Level,
    DateTimeOffset FirstSeen, DateTimeOffset LastSeen, int Count, string? SampleTraceId);

/// <summary>Unknown patterns for review (table <c>unknown_templates</c>); upserted on every <c>unknown_template</c> match.</summary>
public interface IUnknownTemplateSink
{
    void Upsert(TemplateInfo template, LogRecord record);
    IReadOnlyList<UnknownTemplate> All();
}

public sealed class InMemoryUnknownTemplateSink : IUnknownTemplateSink
{
    private readonly object _lock = new();
    private readonly Dictionary<string, UnknownTemplate> _rows = new(StringComparer.Ordinal);

    public void Upsert(TemplateInfo template, LogRecord record)
    {
        lock (_lock)
        {
            var level = record.Level.ToString();
            if (_rows.TryGetValue(template.Id, out var row))
                _rows[template.Id] = row with
                {
                    LastSeen = record.Timestamp > row.LastSeen ? record.Timestamp : row.LastSeen,
                    FirstSeen = record.Timestamp < row.FirstSeen ? record.Timestamp : row.FirstSeen,
                    Count = row.Count + 1,
                    Level = record.Level > Enum.Parse<RecordLevel>(row.Level) ? level : row.Level,
                    SampleTraceId = row.SampleTraceId ?? record.TraceId,
                };
            else
                _rows[template.Id] = new UnknownTemplate(
                    template.Id, template.EventName, template.Category, template.ExceptionType, template.TopFrame, template.Discriminator, level,
                    record.Timestamp, record.Timestamp, 1, record.TraceId);
        }
    }

    public IReadOnlyList<UnknownTemplate> All()
    {
        lock (_lock) return _rows.Values.OrderBy(r => r.TemplateId, StringComparer.Ordinal).ToList();
    }
}

/// <summary>
/// Fingerprint dedup state (operations.md 6.8) behind an interface: the SQLite incident store implements it later.
/// The analyser only reads; <see cref="RecordEscalation"/> is called by whoever acts on the decision.
/// </summary>
public interface IIncidentIndex
{
    /// <summary>The incident with this fingerprint that is <c>open</c> or <c>acknowledged</c>.</summary>
    IncidentRef? FindOpen(string fingerprint);

    /// <summary>The most recently closed incident with this fingerprint, with its outcome.</summary>
    IncidentRef? FindLastClosed(string fingerprint);

    /// <summary>Opens a new incident or folds the escalation into the open one; returns the incident and whether it is new.</summary>
    (IncidentRef Incident, bool IsNew) RecordEscalation(Escalation escalation);
}

public sealed class InMemoryIncidentIndex(TimeProvider time) : IIncidentIndex
{
    private readonly object _lock = new();
    private readonly List<IncidentRef> _incidents = [];
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _perDay = new(StringComparer.Ordinal);

    public IncidentRef? FindOpen(string fingerprint)
    {
        lock (_lock) return _incidents.LastOrDefault(i => i.Fingerprint == fingerprint && i.Status is "open" or "acknowledged");
    }

    public IncidentRef? FindLastClosed(string fingerprint)
    {
        lock (_lock) return _incidents.LastOrDefault(i => i.Fingerprint == fingerprint && i.Status == "closed");
    }

    public (IncidentRef Incident, bool IsNew) RecordEscalation(Escalation escalation)
    {
        lock (_lock)
        {
            var open = _incidents.LastOrDefault(i => i.Fingerprint == escalation.Fingerprint && i.Status is "open" or "acknowledged");
            if (open is not null)
            {
                _counts[open.Id]++;
                var updated = Severities.Rank(escalation.Severity) > Severities.Rank(open.Severity) ? open with { Severity = escalation.Severity } : open;
                _incidents[_incidents.IndexOf(open)] = updated;
                return (updated, false);
            }
            var day = time.GetUtcNow().UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var n = _perDay[day] = _perDay.GetValueOrDefault(day) + 1;
            var created = new IncidentRef($"INC-{day}-{n:D4}", escalation.Fingerprint, "open", escalation.Severity, null);
            _incidents.Add(created);
            _counts[created.Id] = 1;
            return (created, true);
        }
    }

    public int CountOf(string incidentId) { lock (_lock) return _counts.GetValueOrDefault(incidentId); }

    /// <summary>Closes an incident with an outcome (tests and simulations).</summary>
    public void Close(string incidentId, string outcome)
    {
        lock (_lock)
        {
            var i = _incidents.FindIndex(x => x.Id == incidentId);
            if (i >= 0) _incidents[i] = _incidents[i] with { Status = "closed", Outcome = outcome };
        }
    }
}
