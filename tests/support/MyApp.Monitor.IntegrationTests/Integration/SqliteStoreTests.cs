using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Store;
using MyApp.Monitor.Tools;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

/// <summary>The SQLite store (operations.md 10.2): schema, migration, WAL, retention, and every seam of the first pass on top of it.</summary>
public class SqliteStoreTests
{
    private TempDir _dir = null!;
    private string DbPath => _dir.File("data/monitor.db");

    [SetUp]
    public void SetUp() => _dir = new TempDir();

    [TearDown]
    public void TearDown()
    {
        SqliteConnection.ClearAllPools();
        _dir.Dispose();
    }

    private SqliteStore Open() => new(SqliteDatabase.Open(DbPath));

    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private static JsonObject Report(string id, string fingerprint = "9c41d27a05be3f18", string status = "open", string severity = "Error", DateTimeOffset? lastSeen = null)
    {
        var report = (JsonObject)JsonNode.Parse(ExampleReport())!;   // the contract's example, fixed up below so a change to its text changes nothing here
        report["trigger"] = "rate";
        report["environment"] = "prod";
        report["count"] = 41;
        report["area"] = "tasks";
        report["signature"] = new JsonObject
        {
            ["kind"] = "keyed", ["id"] = "task.create.access_lost", ["log_key"] = "task.create.access_lost", ["template_id"] = null, ["template"] = null,
        };
        report["id"] = id;
        report["fingerprint"] = fingerprint;
        report["status"] = status;
        report["severity"] = severity;
        report["last_seen"] = SqliteDatabase.Iso(lastSeen ?? Now);
        return report;
    }

    private static string ExampleReport()
    {
        using var stream = typeof(MonitorApp).Assembly.GetManifestResourceStream("incident-report.example.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // ---- schema, migration, WAL ----

    [Test]
    public void The_store_is_created_in_wal_mode_with_every_table_of_the_schema_and_schema_info()
    {
        var store = Open();
        using var c = store.Database.Connect();
        string Scalar(string sql) { using var cmd = c.CreateCommand(); cmd.CommandText = sql; return Convert.ToString(cmd.ExecuteScalar())!; }

        Assert.That(Scalar("PRAGMA journal_mode"), Is.EqualTo("wal"));
        Assert.That(Scalar("SELECT MAX(version) FROM schema_info"), Is.EqualTo("1"));
        var tables = new List<string>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
            using var r = cmd.ExecuteReader();
            while (r.Read()) tables.Add(r.GetString(0));
        }
        Assert.That(tables, Is.EqualTo(new[]
        {
            "counters", "discovered_resources", "incident_events", "incidents", "investigations", "notifications", "schema_info", "seen_records",
            "source_offsets", "source_watermarks", "suppression_windows", "tool_audit", "unknown_templates",
        }));
        Assert.That(File.Exists(DbPath), Is.True);
    }

    [Test]
    public void The_incidents_table_has_the_columns_and_indexes_of_the_specification()
    {
        var store = Open();
        using var c = store.Database.Connect();
        var columns = new List<string>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM pragma_table_info('incidents')";
            using var r = cmd.ExecuteReader();
            while (r.Read()) columns.Add(r.GetString(0));
        }
        Assert.That(columns, Is.EqualTo(new[]
        {
            "id", "fingerprint", "status", "severity", "trigger", "signature_id", "template_id", "environment", "first_seen", "last_seen", "count",
            "verdict_source", "report_json", "previous_incident_id", "created_at", "updated_at", "closed_at", "outcome",
        }));
        var indexes = new List<string>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM pragma_index_list('incidents')";
            using var r = cmd.ExecuteReader();
            while (r.Read()) indexes.Add(r.GetString(0));
        }
        Assert.That(indexes, Does.Contain("ix_incidents_fingerprint_status").And.Contain("ix_incidents_status_severity"));
    }

    [Test]
    public async Task Opening_the_store_again_applies_no_migration_twice_and_keeps_the_data()
    {
        var store = Open();
        await store.CreateAsync(Report("INC-20261106-0001"), "rule", Now, []);
        store.Database.Close();

        var again = Open();
        Assert.That(again.Database.SchemaVersion, Is.EqualTo(1));
        Assert.That((await again.GetAsync("INC-20261106-0001"))!.Severity, Is.EqualTo("Error"));
        using var c = again.Database.Connect();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM schema_info";
        Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.EqualTo(1));
    }

    // ---- incidents ----

    [Test]
    public async Task Incident_ids_count_up_within_a_day_and_restart_with_the_day()
    {
        var store = Open();
        Assert.That(await store.NextIncidentIdAsync(Now), Is.EqualTo("INC-20261106-0001"));
        await store.CreateAsync(Report("INC-20261106-0001"), "rule", Now, []);
        Assert.That(await store.NextIncidentIdAsync(Now), Is.EqualTo("INC-20261106-0002"));
        Assert.That(await store.NextIncidentIdAsync(Now.AddDays(1)), Is.EqualTo("INC-20261107-0001"));
    }

    [Test]
    public async Task Create_derives_the_columns_from_the_report_and_refuses_a_taken_id()
    {
        var store = Open();
        Assert.That(await store.CreateAsync(Report("INC-20261106-0001"), "rule", Now, [("created", new JsonObject { ["x"] = 1 })]), Is.True);
        Assert.That(await store.CreateAsync(Report("INC-20261106-0001"), "rule", Now, []), Is.False);

        var row = (await store.GetAsync("INC-20261106-0001"))!;
        Assert.That((row.Status, row.Severity, row.Trigger, row.SignatureId, row.Environment, row.Count, row.VerdictSource), Is.EqualTo(("open", "Error", "rate", "task.create.access_lost", "prod", 41, "rule")));
        Assert.That(row.Report["id"]!.GetValue<string>(), Is.EqualTo("INC-20261106-0001"));
        Assert.That(row.Area, Is.EqualTo("tasks"));
        Assert.That((await store.EventsAsync(row.Id)).Single().Kind, Is.EqualTo("created"));
    }

    [Test]
    public async Task Update_is_one_transaction_that_rewrites_the_derived_columns_and_the_timeline()
    {
        var store = Open();
        await store.CreateAsync(Report("INC-20261106-0001"), "rule", Now, []);
        var updated = await store.UpdateAsync("INC-20261106-0001", Now.AddMinutes(5), report =>
        {
            report["status"] = "acknowledged";
            report["severity"] = "Critical";
            report["count"] = 42;
            return IncidentChange.Of("status_change", new JsonObject { ["to"] = "acknowledged" });
        });
        Assert.That((updated!.Status, updated.Severity, updated.Count), Is.EqualTo(("acknowledged", "Critical", 42)));
        Assert.That(updated.UpdatedAt, Is.EqualTo(Now.AddMinutes(5)));
        Assert.That((await store.EventsAsync("INC-20261106-0001")).Select(e => e.Kind), Is.EqualTo(new[] { "status_change" }));
        Assert.That(await store.UpdateAsync("INC-NOPE", Now, _ => IncidentChange.None), Is.Null);
    }

    [Test]
    public async Task A_failing_mutation_changes_nothing()
    {
        var store = Open();
        await store.CreateAsync(Report("INC-20261106-0001"), "rule", Now, []);
        Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateAsync("INC-20261106-0001", Now, r => { r["severity"] = "Critical"; throw new InvalidOperationException(); }));
        Assert.That((await store.GetAsync("INC-20261106-0001"))!.Severity, Is.EqualTo("Error"));
        Assert.That((await store.UpdateAsync("INC-20261106-0001", Now, r => IncidentChange.None))!.Severity, Is.EqualTo("Error"), "the store is not left locked");
    }

    [Test]
    public async Task The_fingerprint_index_finds_the_open_incident_and_the_last_closed_one_with_its_outcome()
    {
        var store = Open();
        IIncidentIndex index = store;
        await store.CreateAsync(Report("INC-20261106-0001", status: "closed"), "rule", Now, []);
        Assert.That(index.FindOpen("9c41d27a05be3f18"), Is.Null);
        await store.UpdateAsync("INC-20261106-0001", Now, r =>
        {
            r["outcome"] = new JsonObject { ["value"] = "noise", ["ticket"] = null, ["pull_request"] = null, ["closed_at"] = SqliteDatabase.Iso(Now), ["closed_by"] = "ann", ["note"] = null };
            return IncidentChange.None;
        });
        Assert.That(index.FindLastClosed("9c41d27a05be3f18")!.Outcome, Is.EqualTo("noise"));

        await store.CreateAsync(Report("INC-20261106-0002"), "rule", Now, []);
        var open = index.FindOpen("9c41d27a05be3f18")!;
        Assert.That((open.Id, open.Status), Is.EqualTo(("INC-20261106-0002", "open")));
        await store.UpdateAsync("INC-20261106-0002", Now, r => { r["status"] = "acknowledged"; return IncidentChange.None; });
        Assert.That(index.FindOpen("9c41d27a05be3f18")!.Status, Is.EqualTo("acknowledged"));
    }

    [Test]
    public async Task List_filters_by_status_severity_trigger_verdict_source_and_area_and_counts_by_status()
    {
        var store = Open();
        await store.CreateAsync(Report("INC-20261106-0001", "aaaaaaaaaaaaaaaa", severity: "Warning"), "rule", Now, []);
        await store.CreateAsync(Report("INC-20261106-0002", "bbbbbbbbbbbbbbbb", severity: "Critical"), "none", Now, []);
        await store.CreateAsync(Report("INC-20261106-0003", "cccccccccccccccc", status: "closed"), "rule", Now, []);

        Assert.That((await store.ListAsync(new IncidentFilter())).Select(i => i.Id), Is.EqualTo(new[] { "INC-20261106-0002", "INC-20261106-0003", "INC-20261106-0001" }), "Critical first");
        Assert.That((await store.ListAsync(new IncidentFilter(Severity: "Warning"))).Select(i => i.Id), Is.EqualTo(new[] { "INC-20261106-0001" }));
        Assert.That((await store.ListAsync(new IncidentFilter(VerdictSource: "none"))).Select(i => i.Id), Is.EqualTo(new[] { "INC-20261106-0002" }));
        Assert.That((await store.ListAsync(new IncidentFilter(Status: "closed"))).Single().Id, Is.EqualTo("INC-20261106-0003"));
        Assert.That((await store.ListAsync(new IncidentFilter(Trigger: "rate"))).Count, Is.EqualTo(3));
        Assert.That((await store.ListAsync(new IncidentFilter(Area: "tasks"))).Count, Is.EqualTo(3));
        Assert.That((await store.ListAsync(new IncidentFilter(Area: "other"))), Is.Empty);
        var counts = await store.CountByStatusAsync();
        Assert.That((counts["open"], counts["acknowledged"], counts["closed"]), Is.EqualTo((2, 0, 1)));
    }

    // ---- seams ----

    [Test]
    public void Counters_are_hourly_buckets_that_add_up_and_survive_a_restart()
    {
        var store = Open();
        var hour = new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);
        store.Increment("task.create.access_lost", "prod", "fp1", hour, 1);
        store.Increment("task.create.access_lost", "prod", "fp1", hour, 2);
        store.Increment("task.create.access_lost", "prod", "fp1", hour.AddHours(1), 1);
        store.Database.Close();

        var rows = Open().Snapshot();
        Assert.That(rows.Select(r => (r.BucketStart.Hour, r.Count)), Is.EqualTo(new[] { (10, 3), (11, 1) }));
    }

    [Test]
    public async Task Counter_totals_split_the_last_hour_from_the_total()
    {
        var store = Open();
        var now = new DateTimeOffset(2026, 11, 6, 12, 30, 0, TimeSpan.Zero);
        store.Increment("a", "prod", "f", new DateTimeOffset(2026, 11, 6, 9, 0, 0, TimeSpan.Zero), 4);
        store.Increment("a", "prod", "f", new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero), 2);
        var total = (await store.CounterTotalsAsync(now)).Single();
        Assert.That((total.Total, total.LastHour), Is.EqualTo((6, 2)));
    }

    [Test]
    public void Unknown_templates_are_upserted_with_the_highest_level_and_the_widest_window()
    {
        var store = Open();
        var template = TemplateExtractor.Extract(Fx.Record("boom", RecordLevel.Warning, category: "Vendor.X"));
        var first = Fx.Record("boom", RecordLevel.Warning, category: "Vendor.X", at: Now, traceId: "4bf92f3577b34da6a3ce929d0e0e4736");
        store.Upsert(template, first);
        store.Upsert(template, Fx.Record("boom", RecordLevel.Error, category: "Vendor.X", at: Now.AddMinutes(-10)));
        store.Upsert(template, Fx.Record("boom", RecordLevel.Information, category: "Vendor.X", at: Now.AddMinutes(10)));

        var row = Open().All().Single();
        Assert.That((row.TemplateId, row.Count, row.Level), Is.EqualTo((template.Id, 3, "Error")));
        Assert.That((row.FirstSeen, row.LastSeen), Is.EqualTo((Now.AddMinutes(-10), Now.AddMinutes(10))));
        Assert.That(row.SampleTraceId, Is.EqualTo("4bf92f3577b34da6a3ce929d0e0e4736"));
    }

    [Test]
    public async Task The_tool_audit_sink_writes_one_row_per_call_and_reads_back_by_incident()
    {
        var store = Open();
        await store.WriteAsync(new ToolAuditRow(Now, "INC-1", "rules", "health_status", "{}", "api", "ok", 2, 100, 7, null), default);
        await store.WriteAsync(new ToolAuditRow(Now, null, "dashboard:ann", "logs_by_correlation", "{\"trace_id\":\"x\"}", "applogs", "denied", 0, 0, 1, "invalid"), default);
        var rows = await store.ToolAuditAsync("INC-1");
        Assert.That(rows.Single().Caller, Is.EqualTo("rules"));
        Assert.That((await store.ToolAuditAsync()).Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Source_offsets_watermarks_seen_records_and_discovery_persist_across_restarts()
    {
        var store = Open();
        await store.SaveOffsetsAsync("applogs", [], [new FileOffset("applogs", "h1", "/logs/a.jsonl", 1234, Now), new FileOffset("applogs", "h2", "/logs/b.jsonl", 55, Now)], default);
        await store.SaveOffsetsAsync("applogs", ["h1"], [new FileOffset("applogs", "h3", "/logs/a.jsonl", 2000, Now)], default);
        await store.SetWatermarkAsync("cloud", "2026-11-06T10:00:00Z", default);
        Assert.That(await store.TryMarkSeenAsync("applogs", "r1", Now, default), Is.True);
        await store.SetDiscoveredAsync(new DiscoveredResource("cloud", "/subscriptions/x/workspaces/w", Now), default);
        store.Database.Close();

        var again = Open();
        var offsets = await again.GetOffsetsAsync("applogs", default);
        Assert.That(offsets.Select(o => (o.HeadHash, o.Offset)), Is.EquivalentTo(new[] { ("h2", 55L), ("h3", 2000L) }));
        Assert.That(await again.GetWatermarkAsync("cloud", default), Is.EqualTo("2026-11-06T10:00:00Z"));
        Assert.That(await again.TryMarkSeenAsync("applogs", "r1", Now, default), Is.False, "a record seen before the restart is a duplicate");
        Assert.That(await again.TryMarkSeenAsync("other", "r1", Now, default), Is.True, "the record id is per source");
        Assert.That((await again.GetDiscoveredAsync("cloud", default))!.ResourceId, Is.EqualTo("/subscriptions/x/workspaces/w"));
        Assert.That((await again.AllOffsetsAsync()).Count, Is.EqualTo(2));
        Assert.That((await again.AllWatermarksAsync())["cloud"], Is.EqualTo("2026-11-06T10:00:00Z"));
    }

    [Test]
    public void Recording_an_escalation_is_not_the_stores_job_the_processor_writes_incidents_with_a_report()
    {
        IIncidentIndex index = Open();
        var e = new Escalation(Fx.Record(), new MatchResult(MatchKind.UnknownKey, null, "x.y.z", null), "unknown", "Error", true, "fp", "x.y.z", 1, 1, 1, 1, EscalationAction.OpenNew, null, null);
        Assert.Throws<NotSupportedException>(() => index.RecordEscalation(e));
    }

    // ---- notifications, windows, investigations ----

    [Test]
    public async Task Notifications_windows_and_investigations_round_trip()
    {
        var store = Open();
        var id = await store.AddNotificationAsync(new NotificationRow(0, "INC-1", "email", "draft", Now, null, null, null, "body", "created"));
        await store.UpdateNotificationAsync(id, "sent", Now.AddMinutes(1), "support@example.test", "ref-1", null);
        var row = (await store.GetNotificationAsync(id))!;
        Assert.That((row.Kind, row.Target, row.Reference, row.Body, row.Event), Is.EqualTo(("sent", "support@example.test", "ref-1", "body", "created")));
        Assert.That((await store.FindNotificationAsync("INC-1", "email", "created"))!.Id, Is.EqualTo(id));
        Assert.That(await store.FindNotificationAsync("INC-1", "email", "severity_raised"), Is.Null);

        await store.AddSuppressionWindowAsync(new SuppressionWindowRow(0, "deploy", Now, Now.AddHours(1), "task.*", "ann", "deploy"));
        Assert.That((await store.SuppressionWindowsAsync()).Single().Match, Is.EqualTo("task.*"));

        await store.SetInvestigationAsync("INC-1", "queued", Now);
        await store.SetInvestigationAsync("INC-1", "running", Now.AddSeconds(5));
        await store.SetInvestigationAsync("INC-1", "failed", Now.AddSeconds(9), "boom");
        var inv = (await store.GetInvestigationAsync("INC-1"))!;
        Assert.That((inv.Status, inv.Attempts, inv.Error), Is.EqualTo(("failed", 1, "boom")));
        Assert.That((inv.QueuedAt, inv.StartedAt, inv.FinishedAt), Is.EqualTo((Now, Now.AddSeconds(5), Now.AddSeconds(9))));
    }

    // ---- retention ----

    [Test]
    public async Task Retention_removes_old_closed_incidents_and_their_rows_and_old_counters_and_seen_records_and_keeps_the_rest()
    {
        var store = Open();
        var old = Now.AddDays(-100);
        JsonObject Closed(string id, string fp, DateTimeOffset closedAt)
        {
            var r = Report(id, fp, status: "closed");
            r["outcome"] = new JsonObject { ["value"] = "fixed", ["ticket"] = null, ["pull_request"] = null, ["closed_at"] = SqliteDatabase.Iso(closedAt), ["closed_by"] = "ann", ["note"] = null };
            return r;
        }
        await store.CreateAsync(Closed("INC-20260801-0001", "aaaaaaaaaaaaaaaa", old), "rule", old, [("created", new JsonObject())]);
        await store.CreateAsync(Closed("INC-20261105-0001", "bbbbbbbbbbbbbbbb", Now.AddDays(-1)), "rule", Now, [("created", new JsonObject())]);
        await store.CreateAsync(Report("INC-20260801-0002", "cccccccccccccccc"), "rule", old, []);   // open and old: never purged
        await store.AddNotificationAsync(new NotificationRow(0, "INC-20260801-0001", "email", "sent", old, null, null, null, null, "created"));
        await store.AddNotificationAsync(new NotificationRow(0, "INC-20261105-0001", "email", "sent", Now, null, null, null, null, "created"));
        await store.WriteAsync(new ToolAuditRow(old, null, "rules", "t", "{}", null, "ok", 0, 0, 0, null), default);
        await store.WriteAsync(new ToolAuditRow(Now, null, "rules", "t", "{}", null, "ok", 0, 0, 0, null), default);
        store.Increment("a", "prod", "f", Now.AddDays(-31), 1);
        store.Increment("a", "prod", "f", Now.AddDays(-2), 1);
        await store.TryMarkSeenAsync("s", "old", Now.AddHours(-3), default);
        await store.TryMarkSeenAsync("s", "new", Now.AddMinutes(-30), default);

        var result = await store.PurgeAsync(Now, 90);

        Assert.That((result.Incidents, result.Events, result.Notifications, result.ToolAudit, result.Counters, result.SeenRecords), Is.EqualTo((1, 1, 1, 1, 1, 1)));
        Assert.That((await store.ListAsync(new IncidentFilter())).Select(i => i.Id), Is.EquivalentTo(new[] { "INC-20261105-0001", "INC-20260801-0002" }));
        Assert.That((await store.NotificationsAsync()).Count, Is.EqualTo(1));
        Assert.That((await store.ToolAuditAsync()).Count, Is.EqualTo(1));
        Assert.That(store.Snapshot().Count, Is.EqualTo(1));
        Assert.That(await store.TryMarkSeenAsync("s", "old", Now, default), Is.True, "purged");
        Assert.That(await store.TryMarkSeenAsync("s", "new", Now, default), Is.False, "kept");
    }

    // ---- the log file source on the persistent store ----

    [Test]
    public async Task The_log_file_source_resumes_from_its_persistent_offsets_after_a_restart_and_reads_only_what_was_added()
    {
        var logs = _dir.File("logs");
        Directory.CreateDirectory(logs);
        var file = Path.Combine(logs, "myapp-api-20261106.jsonl");
        string Line(string message) => $$"""{"Timestamp":"2026-11-06T10:00:00.000Z","Level":"Warning","Category":"X","LogKey":"task.create.access_lost","Message":"{{message}}"}""";
        File.WriteAllText(file, Line("one") + "\n" + Line("two") + "\n");

        async Task<List<string>> ReadAll(SqliteStore store)
        {
            var source = new LogFileSource("applogs");
            await source.StartAsync(new SourceContext("applogs", "log_files", "prod",
                (JsonObject)JsonNode.Parse($$"""{"paths":["{{Path.Combine(logs, "*.jsonl").Replace("\\", "\\\\")}}"],"start_at":"beginning"}""")!, _dir.Path, store, TimeProvider.System), default);
            var messages = new List<string>();
            var result = await source.RunOnceAsync(default);
            foreach (var batch in result.Batches)
            {
                messages.AddRange(batch.Records.Select(r => r.Message));
                await source.CommitAsync(batch, default);
            }
            return messages;
        }

        var first = Open();
        Assert.That(await ReadAll(first), Is.EqualTo(new[] { "one", "two" }));
        first.Database.Close();

        Assert.That(await ReadAll(Open()), Is.Empty, "nothing is read twice after a restart");
        File.AppendAllText(file, Line("three") + "\n");
        Assert.That(await ReadAll(Open()), Is.EqualTo(new[] { "three" }));
    }
}
