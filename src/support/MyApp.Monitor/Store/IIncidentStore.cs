using System.Text.Json.Nodes;

namespace MyApp.Monitor.Store;

/// <summary>One row of <c>incidents</c>. The report is the source of truth; the other columns are derived from it for filters and indexes.</summary>
public sealed record IncidentRow(
    string Id, string Fingerprint, string Status, string Severity, string Trigger, string? SignatureId, string? TemplateId, string Environment,
    DateTimeOffset FirstSeen, DateTimeOffset LastSeen, int Count, string VerdictSource, string ReportJson, string? PreviousIncidentId,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ClosedAt, string? Outcome)
{
    public JsonObject Report => (JsonObject)MonitorJson.Parse(ReportJson)!;

    public string? Area => MonitorJson.ScalarText(Report["area"]);
}

public sealed record IncidentEvent(long Id, string IncidentId, DateTimeOffset At, string Kind, string DataJson);

public sealed record NotificationRow(
    long Id, string IncidentId, string Channel, string Kind, DateTimeOffset At, string? Target, string? Reference, string? Error, string? Body, string Event);

public sealed record SuppressionWindowRow(long Id, string Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Match, string CreatedBy, string? Reason);

public sealed record InvestigationRow(string IncidentId, string Status, int Attempts, DateTimeOffset? QueuedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, string? Error);

public sealed record IncidentFilter(string? Status = null, string? Severity = null, string? Trigger = null, string? VerdictSource = null, string? Area = null);

public sealed record RetentionResult(int Incidents, int Events, int Notifications, int ToolAudit, int Counters, int SeenRecords);

public sealed record CounterTotal(string SignatureOrTemplate, string Environment, int Total, int LastHour, DateTimeOffset LastBucket);

/// <summary>
/// What a change to a report asks the store to record next to it: timeline events (kind and data) written in the same transaction.
/// </summary>
public sealed record IncidentChange(IReadOnlyList<(string Kind, JsonNode Data)> Events)
{
    public static IncidentChange None { get; } = new([]);
    public static IncidentChange Of(string kind, JsonNode data) => new([(kind, data)]);
}

/// <summary>
/// The incident store (operations.md 10.2). A project can add another store kind by implementing this interface; the SQLite
/// store is the one that ships.
/// </summary>
public interface IIncidentStore
{
    /// <summary>The next <c>INC-{yyyyMMdd}-{nnnn}</c> of the day.</summary>
    Task<string> NextIncidentIdAsync(DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Inserts a new incident with its first timeline events. Returns false when the id is taken.</summary>
    Task<bool> CreateAsync(JsonObject report, string verdictSource, DateTimeOffset now, IReadOnlyList<(string Kind, JsonNode Data)> events, CancellationToken ct = default);

    Task<IncidentRow?> GetAsync(string id, CancellationToken ct = default);

    Task<IReadOnlyList<IncidentRow>> ListAsync(IncidentFilter filter, int limit = 500, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Read-modify-write of one report in a transaction: <paramref name="mutate"/> edits the report and says which timeline events to
    /// record; the derived columns are recomputed from the result. Returns null when the incident does not exist.
    /// </summary>
    Task<IncidentRow?> UpdateAsync(string id, DateTimeOffset now, Func<JsonObject, IncidentChange> mutate, CancellationToken ct = default);

    Task<IReadOnlyList<IncidentEvent>> EventsAsync(string incidentId, CancellationToken ct = default);
    Task AddEventAsync(string incidentId, DateTimeOffset at, string kind, JsonNode data, CancellationToken ct = default);

    Task<long> AddNotificationAsync(NotificationRow row, CancellationToken ct = default);
    Task UpdateNotificationAsync(long id, string kind, DateTimeOffset at, string? target, string? reference, string? error, CancellationToken ct = default);
    Task<NotificationRow?> GetNotificationAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationRow>> NotificationsAsync(string? incidentId = null, string? kind = null, int limit = 500, CancellationToken ct = default);
    Task<NotificationRow?> FindNotificationAsync(string incidentId, string channel, string eventName, CancellationToken ct = default);

    Task<long> AddSuppressionWindowAsync(SuppressionWindowRow window, CancellationToken ct = default);
    Task<IReadOnlyList<SuppressionWindowRow>> SuppressionWindowsAsync(CancellationToken ct = default);

    Task SetInvestigationAsync(string incidentId, string status, DateTimeOffset now, string? error = null, CancellationToken ct = default);
    Task<InvestigationRow?> GetInvestigationAsync(string incidentId, CancellationToken ct = default);

    Task<IReadOnlyList<Tools.ToolAuditRow>> ToolAuditAsync(string? incidentId = null, int limit = 200, CancellationToken ct = default);

    Task<IReadOnlyList<CounterTotal>> CounterTotalsAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<IReadOnlyList<Sources.FileOffset>> AllOffsetsAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> AllWatermarksAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Sources.DiscoveredResource>> AllDiscoveredAsync(CancellationToken ct = default);

    /// <summary>Retention (10.2): closed incidents and their rows older than <paramref name="retentionDays"/>, counters older than 30 days, seen records older than 2 hours.</summary>
    Task<RetentionResult> PurgeAsync(DateTimeOffset now, int retentionDays, CancellationToken ct = default);
}
