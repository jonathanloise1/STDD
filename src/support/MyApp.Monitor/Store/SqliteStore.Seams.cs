using System.Globalization;
using Microsoft.Data.Sqlite;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Tools;
using static MyApp.Monitor.Store.SqliteDatabase;

namespace MyApp.Monitor.Store;

// The seams of the first pass, implemented on SQLite: fingerprint index, counters, unknown templates, tool audit, source state.
public sealed partial class SqliteStore
{
    // ---- IIncidentIndex (6.8) ----

    public IncidentRef? FindOpen(string fingerprint) => FindOne(
        "SELECT id, fingerprint, status, severity, outcome FROM incidents WHERE fingerprint = $f AND status IN ('open', 'acknowledged') ORDER BY created_at DESC, id DESC LIMIT 1", fingerprint);

    public IncidentRef? FindLastClosed(string fingerprint) => FindOne(
        "SELECT id, fingerprint, status, severity, outcome FROM incidents WHERE fingerprint = $f AND status = 'closed' ORDER BY closed_at DESC, id DESC LIMIT 1", fingerprint);

    private IncidentRef? FindOne(string sql, string fingerprint)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c, sql, null, ("$f", fingerprint));
        using var r = cmd.ExecuteReader();
        return r.Read() ? new IncidentRef(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), Str(r, "outcome")) : null;
    }

    /// <summary>
    /// Writing an incident needs a report, which the analyser's escalation alone cannot give: the incident processor creates and
    /// updates incidents through <see cref="IIncidentStore"/>. The index is the read side of dedup.
    /// </summary>
    public (IncidentRef Incident, bool IsNew) RecordEscalation(Escalation escalation) =>
        throw new NotSupportedException("The SQLite store writes incidents through IIncidentStore (the incident processor builds the report).");

    // ---- ICounterStore (6.7) ----

    public void Increment(string signatureOrTemplate, string environment, string fingerprint, DateTimeOffset bucketStart, int count)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c,
            "INSERT INTO counters (signature_or_template, environment, fingerprint, bucket_start, count) VALUES ($s, $e, $f, $b, $n) " +
            "ON CONFLICT(signature_or_template, environment, fingerprint, bucket_start) DO UPDATE SET count = count + $n", null,
            ("$s", signatureOrTemplate), ("$e", environment), ("$f", fingerprint), ("$b", Iso(bucketStart)), ("$n", count));
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<CounterRow> Snapshot()
    {
        var rows = new List<CounterRow>();
        using var c = database.Connect();
        using var cmd = Cmd(c, "SELECT signature_or_template, environment, fingerprint, bucket_start, count FROM counters ORDER BY signature_or_template, environment, fingerprint, bucket_start");
        using var r = cmd.ExecuteReader();
        while (r.Read()) rows.Add(new CounterRow(r.GetString(0), r.GetString(1), r.GetString(2), Time(r, "bucket_start"), r.GetInt32(4)));
        return rows;
    }

    public async Task<IReadOnlyList<CounterTotal>> CounterTotalsAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var rows = new List<CounterTotal>();
        using var c = database.Connect();
        using var cmd = Cmd(c,
            "SELECT signature_or_template, environment, SUM(count), SUM(CASE WHEN bucket_start >= $hour THEN count ELSE 0 END), MAX(bucket_start) " +
            "FROM counters GROUP BY signature_or_template, environment ORDER BY SUM(count) DESC, signature_or_template", null,
            ("$hour", Iso(now - TimeSpan.FromHours(1))));
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
            rows.Add(new CounterTotal(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3), ParseIso(r.GetString(4))));
        return rows;
    }

    // ---- IUnknownTemplateSink (6.6) ----

    public void Upsert(TemplateInfo template, LogRecord record)
    {
        var level = record.Level.ToString();
        using var c = database.Connect();
        using var tx = c.BeginTransaction();
        using (var cmd = Cmd(c,
            "INSERT INTO unknown_templates (template_id, event_name, category, exception_type, top_frame, template, level, first_seen, last_seen, count, sample_trace_id) " +
            "VALUES ($id, $ev, $cat, $ex, $top, $tpl, $lvl, $at, $at, 1, $trace) " +
            "ON CONFLICT(template_id) DO UPDATE SET count = count + 1, " +
            "first_seen = MIN(first_seen, $at), last_seen = MAX(last_seen, $at), " +
            "level = CASE WHEN $rank > (CASE level WHEN 'Trace' THEN 0 WHEN 'Debug' THEN 1 WHEN 'Information' THEN 2 WHEN 'Warning' THEN 3 WHEN 'Error' THEN 4 ELSE 5 END) THEN $lvl ELSE level END, " +
            "sample_trace_id = COALESCE(sample_trace_id, $trace)", tx,
            ("$id", template.Id), ("$ev", template.EventName), ("$cat", template.Category), ("$ex", template.ExceptionType), ("$top", template.TopFrame),
            ("$tpl", template.Discriminator), ("$lvl", level), ("$at", Iso(record.Timestamp)), ("$trace", record.TraceId), ("$rank", (int)record.Level)))
            cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public IReadOnlyList<UnknownTemplate> All()
    {
        var rows = new List<UnknownTemplate>();
        using var c = database.Connect();
        using var cmd = Cmd(c,
            "SELECT template_id, event_name, category, exception_type, top_frame, template, level, first_seen, last_seen, count, sample_trace_id FROM unknown_templates ORDER BY template_id");
        using var r = cmd.ExecuteReader();
        while (r.Read())
            rows.Add(new UnknownTemplate(r.GetString(0), Str(r, "event_name"), r.GetString(2), Str(r, "exception_type"), Str(r, "top_frame"), r.GetString(5), r.GetString(6),
                Time(r, "first_seen"), Time(r, "last_seen"), r.GetInt32(9), Str(r, "sample_trace_id")));
        return rows;
    }

    // ---- IToolAuditSink (7.3) ----

    public async Task WriteAsync(ToolAuditRow row, CancellationToken ct)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c,
            "INSERT INTO tool_audit (at, incident_id, caller, tool, arguments_json, source_id, status, rows, output_chars, duration_ms, error) " +
            "VALUES ($at, $inc, $caller, $tool, $args, $src, $status, $rows, $chars, $ms, $err)", null,
            ("$at", Iso(row.At)), ("$inc", row.IncidentId), ("$caller", row.Caller), ("$tool", row.Tool), ("$args", row.ArgumentsJson), ("$src", row.SourceId),
            ("$status", row.Status), ("$rows", row.Rows), ("$chars", row.OutputChars), ("$ms", row.DurationMs), ("$err", row.Error));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    // ---- ISourceStateStore (6.2, 6.4) ----

    public async Task<IReadOnlyList<FileOffset>> GetOffsetsAsync(string sourceId, CancellationToken ct)
    {
        var rows = new List<FileOffset>();
        using var c = database.Connect();
        using var cmd = Cmd(c, "SELECT source_id, head_hash, path, offset, updated_at FROM source_offsets WHERE source_id = $s ORDER BY updated_at, head_hash", null, ("$s", sourceId));
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) rows.Add(new FileOffset(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt64(3), Time(r, "updated_at")));
        return rows;
    }

    public async Task<IReadOnlyList<FileOffset>> AllOffsetsAsync(CancellationToken ct = default)
    {
        var rows = new List<FileOffset>();
        using var c = database.Connect();
        using var cmd = Cmd(c, "SELECT source_id, head_hash, path, offset, updated_at FROM source_offsets ORDER BY source_id, path");
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) rows.Add(new FileOffset(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt64(3), Time(r, "updated_at")));
        return rows;
    }

    public async Task SaveOffsetsAsync(string sourceId, IReadOnlyCollection<string> removeHeadHashes, IReadOnlyCollection<FileOffset> upserts, CancellationToken ct)
    {
        using var c = database.Connect();
        using var tx = c.BeginTransaction();
        foreach (var hash in removeHeadHashes)
        {
            using var cmd = Cmd(c, "DELETE FROM source_offsets WHERE source_id = $s AND head_hash = $h", tx, ("$s", sourceId), ("$h", hash));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        foreach (var o in upserts)
        {
            using var cmd = Cmd(c,
                "INSERT INTO source_offsets (source_id, head_hash, path, offset, updated_at) VALUES ($s, $h, $p, $o, $u) " +
                "ON CONFLICT(source_id, head_hash) DO UPDATE SET path = $p, offset = $o, updated_at = $u", tx,
                ("$s", sourceId), ("$h", o.HeadHash), ("$p", o.Path), ("$o", o.Offset), ("$u", Iso(o.UpdatedAt)));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        tx.Commit();
    }

    public async Task<string?> GetWatermarkAsync(string sourceId, CancellationToken ct)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c, "SELECT watermark FROM source_watermarks WHERE source_id = $s", null, ("$s", sourceId));
        return await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
    }

    public async Task SetWatermarkAsync(string sourceId, string watermark, CancellationToken ct)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c,
            "INSERT INTO source_watermarks (source_id, watermark) VALUES ($s, $w) ON CONFLICT(source_id) DO UPDATE SET watermark = $w", null, ("$s", sourceId), ("$w", watermark));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, string>> AllWatermarksAsync(CancellationToken ct = default)
    {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        using var c = database.Connect();
        using var cmd = Cmd(c, "SELECT source_id, watermark FROM source_watermarks ORDER BY source_id");
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) rows[r.GetString(0)] = r.GetString(1);
        return rows;
    }

    public async Task<bool> TryMarkSeenAsync(string sourceId, string recordId, DateTimeOffset seenAt, CancellationToken ct)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c, "INSERT OR IGNORE INTO seen_records (source_id, record_id, seen_at) VALUES ($s, $r, $at)", null,
            ("$s", sourceId), ("$r", recordId), ("$at", Iso(seenAt)));
        return await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false) > 0;
    }

    public async Task PurgeSeenAsync(DateTimeOffset olderThan, CancellationToken ct)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c, "DELETE FROM seen_records WHERE seen_at < $t", null, ("$t", Iso(olderThan)));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<DiscoveredResource?> GetDiscoveredAsync(string sourceId, CancellationToken ct)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c, "SELECT source_id, resource_id, refreshed_at FROM discovered_resources WHERE source_id = $s", null, ("$s", sourceId));
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false) ? new DiscoveredResource(r.GetString(0), r.GetString(1), Time(r, "refreshed_at")) : null;
    }

    public async Task SetDiscoveredAsync(DiscoveredResource resource, CancellationToken ct)
    {
        using var c = database.Connect();
        using var cmd = Cmd(c,
            "INSERT INTO discovered_resources (source_id, resource_id, refreshed_at) VALUES ($s, $r, $at) " +
            "ON CONFLICT(source_id) DO UPDATE SET resource_id = $r, refreshed_at = $at", null,
            ("$s", resource.SourceId), ("$r", resource.ResourceId), ("$at", Iso(resource.RefreshedAt)));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DiscoveredResource>> AllDiscoveredAsync(CancellationToken ct = default)
    {
        var rows = new List<DiscoveredResource>();
        using var c = database.Connect();
        using var cmd = Cmd(c, "SELECT source_id, resource_id, refreshed_at FROM discovered_resources ORDER BY source_id");
        using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false)) rows.Add(new DiscoveredResource(r.GetString(0), r.GetString(1), Time(r, "refreshed_at")));
        return rows;
    }
}
