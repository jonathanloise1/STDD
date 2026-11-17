using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;

namespace MyApp.Monitor.Analysis;

/// <summary>
/// The analyser (operations.md 6.5 to 6.8): template extraction, matching, rate, severity, escalation, fingerprint and dedup.
/// Deterministic: no model, no clock beyond the injected one, and rates are measured on the records' own timestamps.
/// </summary>
public sealed class Analyser
{
    private static readonly HashSet<string> HealthEvents = new(StringComparer.Ordinal)
    {
        "monitor.api.unreachable", "monitor.api.unhealthy", "platform.health.unhealthy",
    };

    private readonly AnalyserSettings _settings;
    private readonly ISignatureCatalog _catalog;
    private readonly IIncidentIndex _index;
    private readonly ICounterStore _counters;
    private readonly IUnknownTemplateSink _unknowns;
    private readonly TimeProvider _time;
    private readonly object _lock = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _windows = new(StringComparer.Ordinal);
    private DateTimeOffset _latest = DateTimeOffset.MinValue;

    public Analyser(
        AnalyserSettings settings, ISignatureCatalog catalog, IIncidentIndex index, ICounterStore counters, IUnknownTemplateSink unknowns, TimeProvider time)
    {
        _settings = settings;
        _catalog = catalog;
        _index = index;
        _counters = counters;
        _unknowns = unknowns;
        _time = time;
    }

    public static string ComputeFingerprint(string environment, string signatureOrTemplateId, IEnumerable<string> values)
    {
        var text = $"{environment}\n{signatureOrTemplateId}\n{string.Join('|', values)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..16];
    }

    /// <summary>Matching (6.6). Records without a key get their template extracted first.</summary>
    public MatchResult Match(LogRecord record)
    {
        if (record.LogKey is not null)
        {
            var keyed = _catalog.ByLogKey(record.LogKey);
            return keyed is not null
                ? new MatchResult(MatchKind.Matched, keyed, keyed.Id, null)
                : new MatchResult(MatchKind.UnknownKey, null, record.LogKey, null);
        }
        var template = TemplateExtractor.Extract(record);
        var sig = _catalog.ByTemplateId(template.Id);
        return sig is not null
            ? new MatchResult(MatchKind.Matched, sig, sig.Id, template)
            : new MatchResult(MatchKind.UnknownTemplate, null, template.Id, template);
    }

    public AnalysisResult Analyse(LogRecord record)
    {
        lock (_lock) return AnalyseLocked(record);
    }

    private AnalysisResult AnalyseLocked(LogRecord record)
    {
        var match = Match(record);
        var id = match.Id;
        var env = record.Environment;
        var fingerprint = ComputeFingerprint(env, id, (match.Signature?.FingerprintBy ?? []).Select(n => record.Property(n) ?? ""));
        if (record.Timestamp > _latest) _latest = record.Timestamp;

        var window = TimeSpan.FromMinutes(Math.Max(1, _settings.WindowMinutes));
        var sigCount = Count(id + "\n" + env, record.Timestamp, window);
        var fpCount = Count(id + "\n" + env + "\n" + fingerprint, record.Timestamp, window);
        var sigRate = Rate(sigCount);
        var fpRate = Rate(fpCount);

        var bucket = new DateTimeOffset(record.Timestamp.Year, record.Timestamp.Month, record.Timestamp.Day, record.Timestamp.Hour, 0, 0, TimeSpan.Zero);
        _counters.Increment(id, env, fingerprint, bucket, 1);
        if (match.Kind == MatchKind.UnknownTemplate) _unknowns.Upsert(match.Template!, record);

        var decision = Decide(record, match, sigCount, sigRate, out var reason);
        if (decision is null)
            return new AnalysisResult(record, match.Template, match, fingerprint, sigCount, sigRate, null, reason);

        var (trigger, severity, residual) = decision.Value;
        var open = _index.FindOpen(fingerprint);
        var action = open is null ? EscalationAction.OpenNew
            : Severities.Rank(severity) > Severities.Rank(open.Severity) ? EscalationAction.RaiseSeverity : EscalationAction.AppendToOpen;
        string? previous = null;
        if (open is null && _index.FindLastClosed(fingerprint) is { } closed && closed.Outcome is not ("noise" or "duplicate")) previous = closed.Id;

        var escalation = new Escalation(
            record, match, trigger, severity, residual, fingerprint, id, fpCount, fpRate, sigCount, sigRate, action, open, previous);
        return new AnalysisResult(record, match.Template, match, fingerprint, sigCount, sigRate, escalation, null);
    }

    // ---- the escalation table (6.7) ----

    private (string Trigger, string Severity, bool Residual)? Decide(LogRecord record, MatchResult match, int sigCount, double sigRate, out string? reason)
    {
        reason = null;

        // health: always escalates, rule verdict
        if (record.EventName is not null && HealthEvents.Contains(record.EventName))
            return (Triggers.Health, Severities.FromLevel(record.Level), false);

        if (match.Kind == MatchKind.Matched)
        {
            var sig = match.Signature!;
            switch (sig.Status)
            {
                case SignatureStatus.Fixed:
                    if (sig.FixedIn is not null && !SemanticVersion.IsAtLeast(record.Version, sig.FixedIn))
                    {
                        reason = $"fixed in {sig.FixedIn}; the record comes from an older version";
                        return null;
                    }
                    return (Triggers.Regression, Severities.Raise(sig.Severity), false);

                case SignatureStatus.Noise:
                    if (NoiseExpired(sig))
                        return ActiveRules(sig, Severities.AtLeast(sig.Severity, "Warning"), sigCount, sigRate, Triggers.NoiseExpired, out reason);
                    if (OverThreshold(sig, sigCount, sigRate)) return (Triggers.NoiseOutOfContext, sig.Severity, true);
                    reason = "accepted noise within its expected rate";
                    return null;

                default:
                    return ActiveRules(sig, sig.Severity, sigCount, sigRate, null, out reason);
            }
        }

        // unknown_key / unknown_template
        var severity = Severities.FromLevel(record.Level);
        if (record.Level >= RecordLevel.Error) return (Triggers.Unknown, severity, true);
        if (record.Level == RecordLevel.Warning)
        {
            if (sigCount >= _settings.UnknownWarningMinCount) return (Triggers.Unknown, severity, true);
            reason = $"unknown at Warning with {sigCount} in the window (needs {_settings.UnknownWarningMinCount})";
            return null;
        }
        reason = "unknown below Warning: counted only";
        return null;
    }

    private (string Trigger, string Severity, bool Residual)? ActiveRules(
        Signature sig, string severity, int sigCount, double sigRate, string? triggerOverride, out string? reason)
    {
        reason = null;
        if (severity is "Error" or "Critical") return (triggerOverride ?? Triggers.Severity, severity, false);
        if (OverThreshold(sig, sigCount, sigRate)) return (triggerOverride ?? Triggers.Rate, severity, true);
        reason = $"{severity} within its expected rate";
        return null;
    }

    private bool OverThreshold(Signature sig, int count, double rate) =>
        rate > Math.Max(sig.Rate.ExpectedPerHour, 1) * _settings.RateMultiplier && count >= _settings.RateMinCount;

    private bool NoiseExpired(Signature sig)
    {
        if (sig.Noise is null || !DateOnly.TryParseExact(sig.Noise.Expires, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expires)) return true;
        return expires < DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
    }

    // ---- sliding windows ----

    private int Count(string key, DateTimeOffset at, TimeSpan window)
    {
        if (!_windows.TryGetValue(key, out var queue)) _windows[key] = queue = new Queue<DateTimeOffset>();
        var cutoff = _latest - window;
        if (at > cutoff) queue.Enqueue(at);
        while (queue.Count > 0 && queue.Peek() <= cutoff) queue.Dequeue();
        return queue.Count;
    }

    private double Rate(int count) => count * 60.0 / Math.Max(1, _settings.WindowMinutes);
}

/// <summary>Counts per hour, rounded for display.</summary>
public static class RateFormat
{
    public static string Format(double rate) => rate.ToString("0.##", CultureInfo.InvariantCulture);
}
