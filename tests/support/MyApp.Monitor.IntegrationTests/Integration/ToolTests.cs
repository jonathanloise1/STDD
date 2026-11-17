using System.Net;
using System.Text.Json.Nodes;
using MyApp.Monitor.Masking;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Tools;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

/// <summary>The five working example tools (operations.md 7.4) through the real executor, against fakes and fixtures.</summary>
public class ToolTests
{
    private const string OrgId = "0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91";
    private const string UserId = "6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72";
    private const string Trace = "4bf92f3577b34da6a3ce929d0e0e4736";

    private FixtureRepo _repo = null!;
    private OperationsPack _pack = null!;
    private FakeTime _time = null!;
    private SqliteReadRunner _sql = null!;
    private FakeLogAnalyticsClient _la = null!;
    private RecordingAuditSink _audit = null!;
    private TempDir _dir = null!;

    [OneTimeSetUp]
    public void BuildPack()
    {
        _repo = new FixtureRepo();
        _pack = _repo.BuildAndLoad();
    }

    [OneTimeTearDown]
    public void Cleanup() => _repo.Dispose();

    [SetUp]
    public void SetUp()
    {
        _time = FakeTime.Default();
        _sql = new SqliteReadRunner();
        _la = new FakeLogAnalyticsClient();
        _audit = new RecordingAuditSink();
        _dir = new TempDir();
    }

    [TearDown]
    public void TearDown()
    {
        _sql.Dispose();
        _dir.Dispose();
    }

    private ToolServices Services(IHttpClientFactory? http = null) =>
        new(_pack.Entities, _pack.Masking, new StaticPackProvider(_pack), _sql, _la, new FakeWorkspaceResolver("ws-1"), http, _dir.Path, _time);

    private ToolExecutor Executor(ToolBindings bindings, IHttpClientFactory? http = null) =>
        new(_pack.Tools, _pack.Entities, _pack.Constraints, Fx.Masker(), bindings, BuiltInTools.Create(Services(http)), [_audit], _time);

    private ToolExecutor World(string? directory = null) => Executor(ToolBindings.ForWorld(_pack.Tools, directory ?? FixtureRepo.WorldRoot));

    private static async Task<ToolEnvelope> Call(ToolExecutor e, string tool, string args, IncidentToolSession? session = null) =>
        await e.ExecuteAsync(new ToolRequest(tool, Fx.Obj(args), "test", session ?? new IncidentToolSession("INC-1")));

    private static SourceBinding Bind(string id, string kind, JsonObject? settings = null) => new(id, kind, settings ?? new JsonObject());

    private ToolBindings Sql() => new(new Dictionary<string, SourceBinding>
    {
        ["sql_entity_read"] = Bind("main-db", "sqlserver"), ["audit_trail_by_entity"] = Bind("main-db", "sqlserver"),
    });

    // ---- logs_by_correlation ----

    [Test]
    public async Task Logs_over_a_world_return_the_trace_oldest_first_masked_with_the_documented_fields()
    {
        var env = await Call(World(), "logs_by_correlation", $$"""{"trace_id":"{{Trace}}"}""");
        Assert.That(env.Status, Is.EqualTo("ok"));
        Assert.That(env.Rows, Is.EqualTo(2));
        Assert.That(env.Source, Is.EqualTo("world"));
        Assert.That(env.EvidenceId, Is.EqualTo("E2"), "E1 is the triggering signal");
        var records = (JsonArray)env.Data!["records"]!;
        Assert.That(records.Select(r => r!["log_key"]?.GetValue<string>()), Is.EqualTo(new[] { "task.create.access_lost", null }));
        var first = (JsonObject)records[0]!;
        Assert.That(first["kind"]!.GetValue<string>(), Is.EqualTo("trace"));
        Assert.That(first["level"]!.GetValue<string>(), Is.EqualTo("Warning"));
        Assert.That(first["category"]!.GetValue<string>(), Is.EqualTo("MyApp.Application.Services.TaskItemService"));
        Assert.That(first["template"]!.GetValue<string>(), Does.Contain("{UserId}"));
        Assert.That(first["message"]!.GetValue<string>(), Does.Contain("[email]").And.Not.Contain("john.doe"));
        Assert.That(first["span_id"]!.GetValue<string>(), Is.EqualTo("00f067aa0ba902b7"));
    }

    [Test]
    public async Task Logs_for_an_unknown_trace_are_empty()
    {
        var env = await Call(World(), "logs_by_correlation", """{"trace_id":"00000000000000000000000000000000"}""");
        Assert.That(env.Status, Is.EqualTo("empty"));
        Assert.That(env.Rows, Is.Zero);
    }

    [Test]
    public async Task Logs_honour_the_limit_argument()
    {
        var env = await Call(World(), "logs_by_correlation", $$"""{"trace_id":"{{Trace}}","limit":1}""");
        Assert.That(env.Rows, Is.EqualTo(1));
    }

    [Test]
    public async Task Logs_take_a_trace_id_in_exactly_32_lower_case_hex_characters()
    {
        foreach (var bad in new[] { "XYZ", "4BF92F3577B34DA6A3CE929D0E0E4736", Trace + "0", "" })
        {
            var env = await Call(World(), "logs_by_correlation", $$"""{"trace_id":"{{bad}}"}""");
            Assert.That(env.Status, Is.EqualTo("denied"), bad);
        }
        Assert.That((await Call(World(), "logs_by_correlation", $$"""{"trace_id":"{{Trace}}","limit":0}""")).Status, Is.EqualTo("denied"));
        Assert.That((await Call(World(), "logs_by_correlation", $$"""{"trace_id":"{{Trace}}","limit":201}""")).Status, Is.EqualTo("denied"));
    }

    [Test]
    public async Task Logs_over_log_files_search_newest_file_first_and_respect_the_lookback()
    {
        var recent = Path.Combine(_dir.Path, "app-new.jsonl");
        var old = Path.Combine(_dir.Path, "app-old.jsonl");
        string Line(string ts, string msg) =>
            "{\"Timestamp\":\"" + ts + "\",\"Level\":\"Warning\",\"Category\":\"C\",\"LogKey\":\"a.b.c\",\"Message\":\"" + msg + "\",\"TraceId\":\"" + Trace + "\",\"Properties\":{\"ExceptionType\":\"X\"}}";
        File.WriteAllText(old, Line("2026-10-01T10:00:00Z", "ancient") + "\n");
        File.SetLastWriteTimeUtc(old, new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc));
        File.WriteAllText(recent, Line("2026-11-06T09:00:00Z", "second") + "\n" + Line("2026-11-06T08:00:00Z", "first") + "\n" + $$"""{"Timestamp":"2026-11-06T09:30:00Z","TraceId":"other","Message":"nope"}""" + "\n");
        File.SetLastWriteTimeUtc(recent, new DateTime(2026, 11, 6, 9, 30, 0, DateTimeKind.Utc));

        var bindings = new ToolBindings(new Dictionary<string, SourceBinding>
        {
            ["logs_by_correlation"] = Bind("applogs", "log_files", new JsonObject { ["paths"] = new JsonArray("app-*.jsonl") }),
        });
        var env = await Call(Executor(bindings), "logs_by_correlation", $$"""{"trace_id":"{{Trace}}"}""");
        Assert.That(env.Status, Is.EqualTo("ok"));
        var messages = ((JsonArray)env.Data!["records"]!).Select(r => r!["message"]!.GetValue<string>()).ToList();
        Assert.That(messages, Is.EqualTo(new[] { "first", "second" }), "oldest first, the 72 hour lookback leaves the ancient file out, other traces are ignored");
        Assert.That(((JsonArray)env.Data["records"]!)[0]!["exception_type"]!.GetValue<string>(), Is.EqualTo("X"));
    }

    [Test]
    public async Task Logs_over_log_files_stop_at_the_limit()
    {
        var file = Path.Combine(_dir.Path, "app.jsonl");
        File.WriteAllLines(file, Enumerable.Range(0, 10).Select(i => $$"""{"Timestamp":"2026-11-06T09:0{{i}}:00Z","Message":"m{{i}}","TraceId":"{{Trace}}"}"""));
        File.SetLastWriteTimeUtc(file, new DateTime(2026, 11, 6, 9, 30, 0, DateTimeKind.Utc));
        var bindings = new ToolBindings(new Dictionary<string, SourceBinding> { ["logs_by_correlation"] = Bind("applogs", "log_files", new JsonObject { ["paths"] = new JsonArray("app.jsonl") }) });
        var env = await Call(Executor(bindings), "logs_by_correlation", $$"""{"trace_id":"{{Trace}}","limit":3}""");
        Assert.That(env.Rows, Is.EqualTo(3));
    }

    [Test]
    public async Task Logs_over_log_analytics_run_the_catalog_query_with_validated_values_and_map_the_columns()
    {
        _la.Responses.Enqueue(
        [
            new Dictionary<string, object?>
            {
                ["TimeGenerated"] = DateTimeOffset.Parse("2026-11-06T09:00:00Z"), ["Type"] = "AppTraces", ["SeverityLevel"] = 2, ["Message"] = "mail a@b.io",
                ["Properties"] = """{"LogKey":"task.create.access_lost","CategoryName":"Cat","{OriginalFormat}":"mail {Email}"}""", ["ParentId"] = "00f067aa0ba902b7",
            },
            new Dictionary<string, object?>
            {
                ["TimeGenerated"] = DateTimeOffset.Parse("2026-11-06T09:00:01Z"), ["Type"] = "AppExceptions", ["SeverityLevel"] = 3, ["OuterMessage"] = "boom",
                ["ExceptionType"] = "System.Exception", ["Method"] = "MyApp.X.Y", ["Properties"] = "{}",
            },
            new Dictionary<string, object?> { ["TimeGenerated"] = DateTimeOffset.Parse("2026-11-06T09:00:02Z"), ["Type"] = "AppRequests", ["Name"] = "POST /api/tasks", ["ResultCode"] = "403", ["DurationMs"] = 12.5 },
        ]);
        var bindings = new ToolBindings(new Dictionary<string, SourceBinding> { ["logs_by_correlation"] = Bind("appinsights", "log_analytics", new JsonObject { ["workspace_id"] = "ws-1" }) });
        var env = await Call(Executor(bindings), "logs_by_correlation", $$"""{"trace_id":"{{Trace}}","limit":50}""");

        var (workspace, query) = _la.Calls.Single();
        Assert.That(workspace, Is.EqualTo("ws-1"));
        Assert.That(query, Does.Contain($"OperationId == '{Trace}'").And.Contain("ago(72h)").And.Contain("take 50"));
        Assert.That(query, Does.Not.Contain("{"));

        var records = (JsonArray)env.Data!["records"]!;
        Assert.That(records, Has.Count.EqualTo(3));
        Assert.That(records[0]!["kind"]!.GetValue<string>(), Is.EqualTo("trace"));
        Assert.That(records[0]!["level"]!.GetValue<string>(), Is.EqualTo("Warning"));
        Assert.That(records[0]!["log_key"]!.GetValue<string>(), Is.EqualTo("task.create.access_lost"));
        Assert.That(records[0]!["template"]!.GetValue<string>(), Is.EqualTo("mail {Email}"));
        Assert.That(records[0]!["message"]!.GetValue<string>(), Is.EqualTo("mail [email]"));
        Assert.That(records[1]!["kind"]!.GetValue<string>(), Is.EqualTo("exception"));
        Assert.That(records[1]!["message"]!.GetValue<string>(), Is.EqualTo("boom"), "Message|OuterMessage takes the first non-empty");
        Assert.That(records[1]!["top_frame"]!.GetValue<string>(), Is.EqualTo("MyApp.X.Y"), "Properties.TopFrame|Method");
        Assert.That(records[2]!["kind"]!.GetValue<string>(), Is.EqualTo("request"));
        Assert.That(records[2]!["name"]!.GetValue<string>(), Is.EqualTo("POST /api/tasks"));
        Assert.That(records[2]!["result_code"]!.GetValue<string>(), Is.EqualTo("403"));
        Assert.That(records[2]!["duration_ms"]!.GetValue<double>(), Is.EqualTo(12.5));
    }

    // ---- sql_entity_read ----

    [Test]
    public async Task An_entity_read_selects_only_the_allowed_columns_of_one_record_by_a_typed_key()
    {
        var env = await Call(Executor(Sql()), "sql_entity_read", $$"""{"entity":"organization","id":"{{OrgId}}"}""");
        Assert.That(env.Status, Is.EqualTo("ok"));
        Assert.That(env.Data!["found"]!.GetValue<bool>(), Is.True);
        Assert.That(env.Data["entity"]!.GetValue<string>(), Is.EqualTo("organization"));
        var fields = (JsonObject)env.Data["fields"]!;
        Assert.That(fields.Select(f => f.Key), Is.EquivalentTo(new[] { "Id", "Name", "IsDeleted", "BillingCountryCode", "CreatedAt", "CreatedBy", "UpdatedAt", "ModifiedBy" }));
        Assert.That(fields.ContainsKey("InternalNote"), Is.False);

        var statement = _sql.Statements.Single();
        Assert.That(statement, Does.StartWith("SELECT TOP (1) [Id], [Name]"));
        Assert.That(statement, Does.Contain("FROM [dbo].[Organizations] WHERE [Id] = @id"));
        Assert.That(statement, Does.Not.Contain("InternalNote").And.Not.Contain("*"));
        var p = _sql.Parameters.Single().Single();
        Assert.That(p.Name, Is.EqualTo("@id"));
        Assert.That(p.Value, Is.EqualTo(Guid.Parse(OrgId)), "bound as a typed parameter, not text");
        Assert.That(statement, Does.Not.Contain(OrgId), "the value is never in the statement text");
    }

    [Test]
    public async Task An_entity_read_of_a_missing_record_says_not_found_with_status_empty()
    {
        var env = await Call(Executor(Sql()), "sql_entity_read", """{"entity":"organization","id":"11111111-1111-4111-8111-111111111111"}""");
        Assert.That(env.Status, Is.EqualTo("empty"));
        Assert.That(env.Data!["found"]!.GetValue<bool>(), Is.False);
        Assert.That(env.Data["fields"], Is.Null);
    }

    [Test]
    public async Task An_entity_read_refuses_unknown_entities_and_keys_of_the_wrong_type()
    {
        Assert.That((await Call(Executor(Sql()), "sql_entity_read", $$"""{"entity":"payroll","id":"{{OrgId}}"}""")).Status, Is.EqualTo("denied"));
        var bad = await Call(Executor(Sql()), "sql_entity_read", """{"entity":"organization","id":"1; DROP TABLE x"}""");
        Assert.That(bad.Status, Is.EqualTo("denied"));
        Assert.That(_sql.Statements, Is.Empty, "nothing reached the database");
    }

    [Test]
    public async Task An_entity_read_over_a_world_projects_the_allowed_fields_and_hashes_the_email()
    {
        var env = await Call(World(), "sql_entity_read", $$"""{"entity":"user","id":"{{UserId}}"}""");
        var fields = (JsonObject)env.Data!["fields"]!;
        Assert.That(fields["Email"]!.GetValue<string>(), Does.Match("^h:[0-9a-f]{12}$"));
        Assert.That(fields.ContainsKey("PasswordHash"), Is.False);
        Assert.That(fields["PreferredLanguage"]!.GetValue<string>(), Is.EqualTo("en"));
        Assert.That(fields["HasCompletedOnboarding"]!.GetValue<bool>(), Is.True);
        Assert.That(env.FieldsDropped, Is.Zero, "the tool selected only allowed fields");
    }

    [Test]
    public async Task An_entity_read_over_a_world_masks_free_text_and_drops_internal_columns()
    {
        var env = await Call(World(), "sql_entity_read", $$"""{"entity":"organization","id":"{{OrgId}}"}""");
        var fields = (JsonObject)env.Data!["fields"]!;
        Assert.That(fields["Name"]!.GetValue<string>(), Is.EqualTo("Acme Srl (contact [email])"));
        Assert.That(fields.ContainsKey("InternalNote"), Is.False);
        Assert.That(fields["IsDeleted"]!.GetValue<bool>(), Is.True);
    }

    [Test]
    public async Task An_entity_that_does_not_exist_in_the_world_is_not_found()
    {
        var env = await Call(World(), "sql_entity_read", """{"entity":"task","id":"11111111-1111-4111-8111-111111111111"}""");
        Assert.That(env.Data!["found"]!.GetValue<bool>(), Is.False);
    }

    // ---- audit_trail_by_entity ----

    [Test]
    public async Task The_audit_trail_over_sql_lists_newest_first_with_values_filtered_by_the_entity_section()
    {
        var env = await Call(Executor(Sql()), "audit_trail_by_entity", $$"""{"entity":"organization","id":"{{OrgId}}"}""");
        Assert.That(env.Status, Is.EqualTo("ok"));
        var changes = (JsonArray)env.Data!["changes"]!;
        Assert.That(changes, Has.Count.EqualTo(2));
        Assert.That(changes[0]!["action"]!.GetValue<string>(), Is.EqualTo("Update"));
        Assert.That(changes[1]!["action"]!.GetValue<string>(), Is.EqualTo("Create"));
        Assert.That(changes[0]!["changed_properties"]!.AsArray().Select(x => x!.GetValue<string>()), Is.EqualTo(new[] { "IsDeleted" }));
        Assert.That(changes[0]!["correlation_id"]!.GetValue<string>(), Is.EqualTo(new string('b', 32)));
        var newValues = (JsonObject)changes[0]!["new_values"]!;
        Assert.That(newValues["IsDeleted"]!.GetValue<bool>().ToString().ToLowerInvariant(), Is.EqualTo("true"));
        Assert.That(newValues.ContainsKey("InternalNote"), Is.False, "not in the entity's masking section");
        Assert.That(changes[1]!["old_values"], Is.Null);
        Assert.That(env.FieldsDropped, Is.GreaterThanOrEqualTo(2));

        var statement = _sql.Statements.Single();
        Assert.That(statement, Does.StartWith("SELECT TOP (20)").And.Contain("FROM [dbo].[AuditLogs]").And.Contain("[EntityType] = @type AND [EntityId] = @id").And.EndWith("ORDER BY [Timestamp] DESC"));
        Assert.That(_sql.Parameters.Single().Select(p => p.Value), Is.EqualTo(new object[] { "Organization", OrgId }));
    }

    [Test]
    public async Task The_audit_trail_limit_is_bounded_by_the_schema_and_applied()
    {
        var env = await Call(Executor(Sql()), "audit_trail_by_entity", $$"""{"entity":"organization","id":"{{OrgId}}","limit":1}""");
        Assert.That(env.Rows, Is.EqualTo(1));
        Assert.That(_sql.Statements.Single(), Does.StartWith("SELECT TOP (1)"));
        Assert.That((await Call(Executor(Sql()), "audit_trail_by_entity", $$"""{"entity":"organization","id":"{{OrgId}}","limit":51}""")).Status, Is.EqualTo("denied"));
    }

    [Test]
    public async Task The_audit_trail_over_a_world_reads_audit_jsonl_for_the_entity_type_and_id()
    {
        var env = await Call(World(), "audit_trail_by_entity", $$"""{"entity":"organization","id":"{{OrgId}}"}""");
        var changes = (JsonArray)env.Data!["changes"]!;
        Assert.That(changes, Has.Count.EqualTo(2));
        Assert.That(changes[0]!["timestamp"]!.GetValue<string>(), Is.EqualTo("2026-11-06T09:10:02Z"));
        Assert.That(changes[0]!["old_values"]!.AsObject().ContainsKey("InternalNote"), Is.False);

        var user = await Call(World(), "audit_trail_by_entity", $$"""{"entity":"user","id":"{{UserId}}"}""");
        Assert.That(((JsonArray)user.Data!["changes"]!).Single()!["changed_properties"]!.AsArray().Single()!.GetValue<string>(), Is.EqualTo("PreferredLanguage"));
        var none = await Call(World(), "audit_trail_by_entity", """{"entity":"task","id":"11111111-1111-4111-8111-111111111111"}""");
        Assert.That(none.Status, Is.EqualTo("empty"));
    }

    // ---- health_status ----

    [Test]
    public async Task Health_over_http_maps_the_backend_document()
    {
        var factory = FakeHttpFactory.Json(HttpStatusCode.ServiceUnavailable,
            """{"status":"Unhealthy","checks":[{"name":"sqlserver","status":"Unhealthy","durationMs":12},{"name":"disk","status":"Healthy","durationMs":1}]}""");
        var bindings = new ToolBindings(new Dictionary<string, SourceBinding> { ["health_status"] = Bind("api", "http_health", new JsonObject { ["base_url"] = "https://localhost:7342" }) });
        var env = await Call(Executor(bindings, factory), "health_status", "{}");
        Assert.That(env.Status, Is.EqualTo("ok"));
        Assert.That(env.Data!["status"]!.GetValue<string>(), Is.EqualTo("Unhealthy"));
        var checks = (JsonArray)env.Data["checks"]!;
        Assert.That(checks[0]!["name"]!.GetValue<string>(), Is.EqualTo("sqlserver"));
        Assert.That(checks[0]!["duration_ms"]!.GetValue<int>(), Is.EqualTo(12));
        Assert.That(env.Rows, Is.EqualTo(2));
    }

    [Test]
    public async Task Health_over_http_reports_an_error_when_the_answer_is_not_a_health_document()
    {
        var bindings = new ToolBindings(new Dictionary<string, SourceBinding> { ["health_status"] = Bind("api", "http_health", new JsonObject { ["base_url"] = "https://x" }) });
        var env = await Call(Executor(bindings, FakeHttpFactory.Json(HttpStatusCode.OK, "<html/>")), "health_status", "{}");
        Assert.That(env.Status, Is.EqualTo("error"));
        Assert.That(env.Error, Does.Contain("without JSON"));
    }

    [Test]
    public async Task Health_over_a_world_reads_health_json()
    {
        var env = await Call(World(), "health_status", "{}");
        Assert.That(env.Data!["status"]!.GetValue<string>(), Is.EqualTo("Degraded"));
        Assert.That(((JsonArray)env.Data["checks"]!).Select(c => c!["status"]!.GetValue<string>()), Is.EqualTo(new[] { "Unhealthy", "Healthy" }));
    }

    [Test]
    public async Task Health_takes_no_arguments()
    {
        Assert.That((await Call(World(), "health_status", """{"verbose":true}""")).Status, Is.EqualTo("denied"));
    }

    // ---- signature_lookup ----

    [Test]
    public async Task Signature_lookup_finds_by_log_key_signature_id_and_template_id_without_call_site_file_and_line()
    {
        foreach (var args in new[]
        {
            """{"log_key":"task.create.access_lost"}""", """{"signature_id":"task.create.access_lost"}""", """{"signature_id":"t-2bb5dcdc1949"}""", """{"template_id":"t-2bb5dcdc1949"}""",
        })
        {
            var env = await Call(World(), "signature_lookup", args);
            Assert.That(env.Status, Is.EqualTo("ok"), args);
            Assert.That(env.Source, Is.EqualTo("pack"));
            Assert.That(env.Data!["found"]!.GetValue<bool>(), Is.True);
        }
        var keyed = await Call(World(), "signature_lookup", """{"log_key":"task.create.access_lost"}""");
        var sig = (JsonObject)keyed.Data!["signature"]!;
        Assert.That(sig["id"]!.GetValue<string>(), Is.EqualTo("task.create.access_lost"));
        Assert.That(sig["user_story"]!.GetValue<string>(), Is.EqualTo("US-TASK-01"));
        Assert.That(sig["runbook"]!["anchor"]!.GetValue<string>(), Is.EqualTo("task-create-access-lost"));
        Assert.That(sig["call_site"]!.AsObject().ContainsKey("file"), Is.False);
        Assert.That(sig["call_site"]!.AsObject().ContainsKey("line"), Is.False);
        Assert.That(sig["call_site"]!["method"]!.GetValue<string>(), Is.EqualTo("TaskCreateAccessLost"));
    }

    [Test]
    public async Task Signature_lookup_of_an_unknown_id_is_empty_and_a_template_id_is_not_a_log_key()
    {
        Assert.That((await Call(World(), "signature_lookup", """{"log_key":"task.create.nothing"}""")).Status, Is.EqualTo("empty"));
        Assert.That((await Call(World(), "signature_lookup", """{"template_id":"t-000000000000"}""")).Data!["found"]!.GetValue<bool>(), Is.False);
    }

    [Test]
    public async Task Signature_lookup_needs_exactly_one_of_the_three_arguments()
    {
        Assert.That((await Call(World(), "signature_lookup", "{}")).Status, Is.EqualTo("denied"));
        Assert.That((await Call(World(), "signature_lookup", """{"log_key":"task.create.access_lost","signature_id":"x"}""")).Status, Is.EqualTo("denied"));
        Assert.That((await Call(World(), "signature_lookup", """{"log_key":"NOT A KEY"}""")).Status, Is.EqualTo("denied"));
    }

    [Test]
    public async Task The_pack_is_the_only_source_of_signature_lookup_and_it_is_not_masked_into_nothing()
    {
        var env = await Call(World(), "signature_lookup", """{"signature_id":"t-2bb5dcdc1949"}""");
        Assert.That(env.Data!["signature"]!["runbook"]!["text"]!.GetValue<string>(), Does.Contain("The database health check failed"));
        Assert.That(env.FieldsDropped, Is.Zero);
    }
}
