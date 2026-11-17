using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Tools;
using static MyApp.Monitor.Store.SqliteDatabase;

namespace MyApp.Monitor.Store;

/// <summary>
/// The SQLite store (operations.md 10.2). One class implements every persistence seam of the monitor: the incident store, the
/// fingerprint index and counters of the analyser, the unknown template sink, the tool audit sink and the source state tables
/// (offsets, watermarks, seen records, discovery).
/// </summary>
public sealed partial class SqliteStore(SqliteDatabase database) :
    IIncidentStore, IIncidentIndex, ICounterStore, IUnknownTemplateSink, IToolAuditSink, ISourceStateStore
{
    public SqliteDatabase Database => database;

    private static readonly SemaphoreSlim IdLock = new(1, 1);

    // ---- helpers ----

    private static SqliteCommand Cmd(SqliteConnection c, string sql, SqliteTransaction? tx = null, params (string Name, object? Value)[] parameters)
    {
        var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    private static string? Str(SqliteDataReader r, string column) => r.IsDBNull(r.GetOrdinal(column)) ? null : r.GetString(r.GetOrdinal(column));
    private static int Int(SqliteDataReader r, string column) => r.GetInt32(r.GetOrdinal(column));
    private static DateTimeOffset Time(SqliteDataReader r, string column) => ParseIso(r.GetString(r.GetOrdinal(column)));
    private static DateTimeOffset? TimeOrNull(SqliteDataReader r, string column) => r.IsDBNull(r.GetOrdinal(column)) ? null : ParseIso(r.GetString(r.GetOrdinal(column)));

    private const string IncidentColumnList =
        "id, fingerprint, status, severity, trigger, signature_id, template_id, environment, first_seen, last_seen, count, verdict_source, report_json, " +
        "previous_incident_id, created_at, updated_at, closed_at, outcome";

    private static IncidentRow ReadIncident(SqliteDataReader r) => new(
        r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), Str(r, "signature_id"), Str(r, "template_id"), r.GetString(7),
        Time(r, "first_seen"), Time(r, "last_seen"), Int(r, "count"), r.GetString(11), r.GetString(12), Str(r, "previous_incident_id"),
        Time(r, "created_at"), Time(r, "updated_at"), TimeOrNull(r, "closed_at"), Str(r, "outcome"));

    /// <summary>The columns derived from a report. The report is the source of truth.</summary>
    public static IncidentColumns Derive(JsonObject report)
    {
        string S(string path) => MonitorJson.ScalarText(MonitorJson.Get(report, path)) ?? "";
        string? N(string path) => MonitorJson.ScalarText(MonitorJson.Get(report, path));
        var verdictSource = MonitorJson.Get(report, "model_verdict") is JsonObject ? "model" : MonitorJson.Get(report, "rule_verdict") is JsonObject ? "rule" : "none";
        var kind = N("signature.kind");
        return new IncidentColumns(
            S("id"), S("fingerprint"), S("status"), S("severity"), S("trigger"),
            kind is "keyed" or "template" ? N("signature.id") : null, N("signature.template_id"), S("environment"),
            ParseIso(S("first_seen")), ParseIso(S("last_seen")), int.Parse(S("count"), CultureInfo.InvariantCulture), verdictSource,
            N("previous_incident_id"), N("outcome.closed_at") is { } closed ? ParseIso(closed) : null, N("outcome.value"));
    }

    public sealed record IncidentColumns(
        string Id, string Fingerprint, string Status, string Severity, string Trigger, string? SignatureId, string? TemplateId, string Environment,
        DateTimeOffset FirstSeen, DateTimeOffset LastSeen, int Count, string VerdictSource, string? PreviousIncidentId, DateTimeOffset? ClosedAt, string? Outcome);

    // ---- incidents ----

    public async Task<string> NextIncidentIdAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var day = now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        await IdLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var c = database.Connect();
            using var cmd = Cmd(c, "SELECT COALESCE(MAX(CAST(SUBSTR(id, 14) AS INTEGER)), 0) FROM incidents WHERE id LIKE $p", null, ("$p", $"INC-{day}-%"));
            var last = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
            // ids already handed out but not yet written are bridged by the reservation below
            var reserved = _reserved.GetValueOrDefault(day);
            var next = Math.Max(last, reserved) + 1;
            _reserved[day] = next;
            return $"INC-{day}-{next:D4}";
        }
        finally { IdLock.Release(); }
    }

    private readonly Dictionary<string, int> _reserved = [];

    public async Task<bool> CreateAsync(
        JsonObject report, string verdictSource, DateTimeOffset now, IReadOnlyList<(string Kind, JsonNode Data)> events, CancellationToken ct = default)
    {
        var d = Derive(report);
        using var c = database.Connect();
        using var tx = c.BeginTransaction();
        using (var cmd = Cmd(c,
            $"INSERT OR IGNORE INTO incidents ({IncidentColumnList}) VALUES ($id, $fp, $status, $sev, $trig, $sig, $tpl, $env, $first, $last, $count, $vs, $json, $prev, $now, $now, $closed, $outcome)", tx,
            ("$id", d.Id), ("$fp", d.Fingerprint), ("$status", d.Status), ("$sev", d.Severity), ("$trig", d.Trigger), ("$sig", d.SignatureId), ("$tpl", d.TemplateId),
            ("$env", d.Environment), ("$first", Iso(d.FirstSeen)), ("$last", Iso(d.LastSeen)), ("$count", d.Count), ("$vs", verdictSource),
            ("$json", MonitorJson.ToCompact(report)), ("$prev", d.PreviousIncidentId), ("$now", Iso(now)),
            ("$closed", d.ClosedAt is null ? null : Iso(d.ClosedAt.Value)), ("$outcome", d.Outcome)))
        {
            if (await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 0) return false;
        }
        foreach (var (kind, data) in events) InsertEvent(c, tx, d.Id, now, kind, data);
        tx.Commit();
        return true;
    }

    private static void InsertEvent(SqliteConnection c, SqliteTransaction tx, string incidentId, DateTimeOffset at, string kind, JsonNode data)
    {
        using var cmd = Cmd(c, "INSERT INTO incident_events (incident_id, at, kind, data_json) VALUES ($i, $at, $k, $d)", tx,
            ("$i", incidentId), ("$at", Iso(at)), ("$k", kind), ("$d", MonitorJson.ToCompact(data)));
        cmd.ExecuteNonQuery();
    }

    public async Task<IncidentRow?> GetAsync(string id, CancellationToken ct = default)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c, $"SELECT {IncidentColumnList} FROM incidents WHERE id = $id", null, ("$id", id));
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false) ? ReadIncident(r) : null;
    }

    public async Task<IReadOnlyList<IncidentRow>> ListAsync(IncidentFilter filter, int limit = 500, CancellationToken ct = default)
    {
        var where = new List<string>();
        var ps = new List<(string, object?)>();
        void Add(string column, string? value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            where.Add($"{column} = {name}");
            ps.Add((name, value));
        }
        Add("status", filter.Status, "$status");
        Add("severity", filter.Severity, "$sev");
        Add("trigger", filter.Trigger, "$trig");
        Add("verdict_source", filter.VerdictSource, "$vs");
        var sql = $"SELECT {IncidentColumnList} FROM incidents" + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "") +
                  " ORDER BY CASE severity WHEN 'Critical' THEN 0 WHEN 'Error' THEN 1 WHEN 'Warning' THEN 2 ELSE 3 END, last_seen DESC, id DESC";
        var rows = new List<IncidentRow>();
        using var c = database.Connect();
        using var cmd = Cmd(c, sql, null, ps.ToArray());
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) rows.Add(ReadIncident(r));
        // the area lives inside the report: filtered after the read, which is cheap at the sizes of an incident list
        if (!string.IsNullOrWhiteSpace(filter.Area)) rows = rows.Where(x => string.Equals(x.Area, filter.Area, StringComparison.Ordinal)).ToList();
        return rows.Take(limit).ToList();
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<string, int> { ["open"] = 0, ["acknowledged"] = 0, ["closed"] = 0 };
        using var c = database.Connect();
        using var cmd = Cmd(c, "SELECT status, COUNT(*) FROM incidents GROUP BY status");
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) result[r.GetString(0)] = r.GetInt32(1);
        return result;
    }

    public async Task<IncidentRow?> UpdateAsync(string id, DateTimeOffset now, Func<JsonObject, IncidentChange> mutate, CancellationToken ct = default)
    {
        using var c = database.Connect();
        // BEGIN IMMEDIATE: the read and the write of a report are one step even with several writers
        using (var begin = Cmd(c, "BEGIN IMMEDIATE")) await begin.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        try
        {
            string json;
            using (var cmd = Cmd(c, "SELECT report_json FROM incidents WHERE id = $id", null, ("$id", id)))
            {
                var value = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                if (value is null or DBNull)
                {
                    using var rollback = Cmd(c, "ROLLBACK");
                    rollback.ExecuteNonQuery();
                    return null;
                }
                json = (string)value;
            }
            var report = (JsonObject)MonitorJson.Parse(json)!;
            var change = mutate(report);
            var d = Derive(report);
            using (var cmd = Cmd(c,
                "UPDATE incidents SET status=$status, severity=$sev, trigger=$trig, signature_id=$sig, template_id=$tpl, first_seen=$first, last_seen=$last, count=$count, " +
                "verdict_source=$vs, report_json=$json, previous_incident_id=$prev, updated_at=$now, closed_at=$closed, outcome=$outcome WHERE id=$id", null,
                ("$id", id), ("$status", d.Status), ("$sev", d.Severity), ("$trig", d.Trigger), ("$sig", d.SignatureId), ("$tpl", d.TemplateId),
                ("$first", Iso(d.FirstSeen)), ("$last", Iso(d.LastSeen)), ("$count", d.Count), ("$vs", d.VerdictSource), ("$json", MonitorJson.ToCompact(report)),
                ("$prev", d.PreviousIncidentId), ("$now", Iso(now)), ("$closed", d.ClosedAt is null ? null : Iso(d.ClosedAt.Value)), ("$outcome", d.Outcome)))
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            foreach (var (kind, data) in change.Events)
            {
                using var cmd = Cmd(c, "INSERT INTO incident_events (incident_id, at, kind, data_json) VALUES ($i, $at, $k, $d)", null,
                    ("$i", id), ("$at", Iso(now)), ("$k", kind), ("$d", MonitorJson.ToCompact(data)));
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            using (var commit = Cmd(c, "COMMIT")) commit.ExecuteNonQuery();
        }
        catch
        {
            try { using var rollback = Cmd(c, "ROLLBACK"); rollback.ExecuteNonQuery(); } catch (SqliteException) { /* already ended */ }
            throw;
        }
        return await GetAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<IncidentEvent>> EventsAsync(string incidentId, CancellationToken ct = default)
    {
        var rows = new List<IncidentEvent>();
        using var c = database.Connect();
        using var cmd = Cmd(c, "SELECT id, incident_id, at, kind, data_json FROM incident_events WHERE incident_id = $i ORDER BY id", null, ("$i", incidentId));
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) rows.Add(new IncidentEvent(r.GetInt64(0), r.GetString(1), Time(r, "at"), r.GetString(3), r.GetString(4)));
        return rows;
    }

    public async Task AddEventAsync(string incidentId, DateTimeOffset at, string kind, JsonNode data, CancellationToken ct = default)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c, "INSERT INTO incident_events (incident_id, at, kind, data_json) VALUES ($i, $at, $k, $d)", null,
            ("$i", incidentId), ("$at", Iso(at)), ("$k", kind), ("$d", MonitorJson.ToCompact(data)));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    // ---- notifications ----

    private const string NotificationColumns = "id, incident_id, channel, kind, at, target, reference, error, body, event";

    private static NotificationRow ReadNotification(SqliteDataReader r) => new(
        r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), Time(r, "at"), Str(r, "target"), Str(r, "reference"), Str(r, "error"), Str(r, "body"), r.GetString(9));

    public async Task<long> AddNotificationAsync(NotificationRow row, CancellationToken ct = default)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c,
            "INSERT INTO notifications (incident_id, channel, kind, at, target, reference, error, body, event) VALUES ($i, $ch, $k, $at, $t, $ref, $err, $body, $ev); SELECT last_insert_rowid()", null,
            ("$i", row.IncidentId), ("$ch", row.Channel), ("$k", row.Kind), ("$at", Iso(row.At)), ("$t", row.Target), ("$ref", row.Reference), ("$err", row.Error),
            ("$body", row.Body), ("$ev", row.Event));
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    public async Task UpdateNotificationAsync(long id, string kind, DateTimeOffset at, string? target, string? reference, string? error, CancellationToken ct = default)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c, "UPDATE notifications SET kind=$k, at=$at, target=COALESCE($t, target), reference=$ref, error=$err WHERE id=$id", null,
            ("$id", id), ("$k", kind), ("$at", Iso(at)), ("$t", target), ("$ref", reference), ("$err", error));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<NotificationRow?> GetNotificationAsync(long id, CancellationToken ct = default)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c, $"SELECT {NotificationColumns} FROM notifications WHERE id = $id", null, ("$id", id));
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false) ? ReadNotification(r) : null;
    }

    public async Task<IReadOnlyList<NotificationRow>> NotificationsAsync(string? incidentId = null, string? kind = null, int limit = 500, CancellationToken ct = default)
    {
        var where = new List<string>();
        var ps = new List<(string, object?)>();
        if (incidentId is not null) { where.Add("incident_id = $i"); ps.Add(("$i", incidentId)); }
        if (kind is not null) { where.Add("kind = $k"); ps.Add(("$k", kind)); }
        var sql = $"SELECT {NotificationColumns} FROM notifications" + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "") + " ORDER BY id DESC LIMIT $limit";
        ps.Add(("$limit", limit));
        var rows = new List<NotificationRow>();
        using var c = database.Connect();
        using var cmd = Cmd(c, sql, null, ps.ToArray());
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) rows.Add(ReadNotification(r));
        return rows;
    }

    public async Task<NotificationRow?> FindNotificationAsync(string incidentId, string channel, string eventName, CancellationToken ct = default)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c, $"SELECT {NotificationColumns} FROM notifications WHERE incident_id=$i AND channel=$ch AND event=$ev ORDER BY id DESC LIMIT 1", null,
            ("$i", incidentId), ("$ch", channel), ("$ev", eventName));
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false) ? ReadNotification(r) : null;
    }

    // ---- suppression windows, investigations, audit reads ----

    public async Task<long> AddSuppressionWindowAsync(SuppressionWindowRow window, CancellationToken ct = default)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c,
            "INSERT INTO suppression_windows (name, starts_at, ends_at, match, created_by, reason) VALUES ($n, $s, $e, $m, $by, $r); SELECT last_insert_rowid()", null,
            ("$n", window.Name), ("$s", Iso(window.StartsAt)), ("$e", Iso(window.EndsAt)), ("$m", window.Match), ("$by", window.CreatedBy), ("$r", window.Reason));
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<SuppressionWindowRow>> SuppressionWindowsAsync(CancellationToken ct = default)
    {
        var rows = new List<SuppressionWindowRow>();
        using var c = database.Connect();
        using var cmd = Cmd(c, "SELECT id, name, starts_at, ends_at, match, created_by, reason FROM suppression_windows ORDER BY starts_at DESC");
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
            rows.Add(new SuppressionWindowRow(r.GetInt64(0), r.GetString(1), Time(r, "starts_at"), Time(r, "ends_at"), r.GetString(4), r.GetString(5), Str(r, "reason")));
        return rows;
    }

    public async Task SetInvestigationAsync(string incidentId, string status, DateTimeOffset now, string? error = null, CancellationToken ct = default)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c,
            "INSERT INTO investigations (incident_id, status, attempts, queued_at, error) VALUES ($i, $s, 0, $now, $e) " +
            "ON CONFLICT(incident_id) DO UPDATE SET status=$s, queued_at=CASE WHEN $s='queued' THEN $now ELSE queued_at END, " +
            "started_at=CASE WHEN $s='running' THEN $now ELSE started_at END, finished_at=CASE WHEN $s IN ('done','abstained','failed','skipped') THEN $now ELSE finished_at END, " +
            "attempts=attempts + CASE WHEN $s='running' THEN 1 ELSE 0 END, error=$e", null,
            ("$i", incidentId), ("$s", status), ("$now", Iso(now)), ("$e", error));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<InvestigationRow?> GetInvestigationAsync(string incidentId, CancellationToken ct = default)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c, "SELECT incident_id, status, attempts, queued_at, started_at, finished_at, error FROM investigations WHERE incident_id=$i", null, ("$i", incidentId));
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false)
            ? new InvestigationRow(r.GetString(0), r.GetString(1), r.GetInt32(2), TimeOrNull(r, "queued_at"), TimeOrNull(r, "started_at"), TimeOrNull(r, "finished_at"), Str(r, "error"))
            : null;
    }

    public async Task<IReadOnlyList<ToolAuditRow>> ToolAuditAsync(string? incidentId = null, int limit = 200, CancellationToken ct = default)
    {
        var rows = new List<ToolAuditRow>();
        using var c = database.Connect();
        using var cmd = Cmd(c,
            "SELECT at, incident_id, caller, tool, arguments_json, source_id, status, rows, output_chars, duration_ms, error FROM tool_audit " +
            (incidentId is null ? "" : "WHERE incident_id = $i ") + "ORDER BY id DESC LIMIT $limit", null, ("$i", incidentId), ("$limit", limit));
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
            rows.Add(new ToolAuditRow(Time(r, "at"), Str(r, "incident_id"), r.GetString(2), r.GetString(3), r.GetString(4), Str(r, "source_id"), r.GetString(6),
                r.GetInt32(7), r.GetInt32(8), r.GetInt64(9), Str(r, "error")));
        return rows;
    }

    // ---- retention ----

    public async Task<RetentionResult> PurgeAsync(DateTimeOffset now, int retentionDays, CancellationToken ct = default)
    {
        var cutoff = Iso(now - TimeSpan.FromDays(retentionDays));
        using var c = database.Connect();
        using var tx = c.BeginTransaction();
        async Task<int> Run(string sql, params (string, object?)[] ps)
        {
            using var cmd = Cmd(c, sql, tx, ps);
            return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        const string old = "SELECT id FROM incidents WHERE status = 'closed' AND closed_at < $cutoff";
        var events = await Run($"DELETE FROM incident_events WHERE incident_id IN ({old})", ("$cutoff", cutoff));
        var notifications = await Run($"DELETE FROM notifications WHERE incident_id IN ({old})", ("$cutoff", cutoff));
        await Run($"DELETE FROM investigations WHERE incident_id IN ({old})", ("$cutoff", cutoff));
        var incidents = await Run("DELETE FROM incidents WHERE status = 'closed' AND closed_at < $cutoff", ("$cutoff", cutoff));
        var audit = await Run("DELETE FROM tool_audit WHERE at < $cutoff", ("$cutoff", cutoff));
        var counters = await Run("DELETE FROM counters WHERE bucket_start < $c", ("$c", Iso(now - TimeSpan.FromDays(30))));
        var seen = await Run("DELETE FROM seen_records WHERE seen_at < $s", ("$s", Iso(now - TimeSpan.FromHours(2))));
        tx.Commit();
        return new RetentionResult(incidents, events, notifications, audit, counters, seen);
    }
}
