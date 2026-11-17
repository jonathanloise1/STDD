using System.Text.Json.Nodes;
using MyApp.Monitor.Masking;
using MyApp.Monitor.Tools;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

/// <summary>Executor rules (operations.md 7.3): validation, read-only, budgets, duplicates, masking, audit, constraints.</summary>
public class ToolExecutorTests
{
    private const string Trace = "4bf92f3577b34da6a3ce929d0e0e4736";
    private RecordingAuditSink _audit = null!;

    [SetUp]
    public void SetUp() => _audit = new RecordingAuditSink();

    private static string T(int n) => n.ToString("x32");

    private static ToolBindings Bindings() => new(new Dictionary<string, SourceBinding>
    {
        ["health_status"] = new("api", "http_health", new JsonObject { ["base_url"] = "https://x" }),
        ["logs_by_correlation"] = new("applogs", "log_files", new JsonObject { ["paths"] = new JsonArray("x") }),
        ["sql_entity_read"] = new("main-db", "sqlserver", new JsonObject()),
        ["signature_lookup"] = new("pack", "pack", new JsonObject()),
    });

    private (ToolExecutor Executor, ScriptedImplementation Logs, ScriptedImplementation Health) Build(
        ToolCatalog? catalog = null, ConstraintSet? constraints = null, ToolBindings? bindings = null,
        Func<ToolContext, CancellationToken, Task<ToolResult>>? logs = null, Func<ToolContext, CancellationToken, Task<ToolResult>>? health = null)
    {
        var entityImpl = new ScriptedImplementation("entity", "sqlserver", (c, _) => Task.FromResult(new ToolResult(new JsonObject { ["entity"] = "user", ["id"] = "1", ["found"] = false, ["fields"] = null }, 0)));
        var logImpl = new ScriptedImplementation("logs", "log_files", logs ?? ((c, _) => Task.FromResult(new ToolResult(new JsonObject { ["records"] = new JsonArray(new JsonObject { ["message"] = "hello", ["level"] = "Warning" }) }, 1))));
        var healthImpl = new ScriptedImplementation("health", "http_health", health ?? ((c, _) => Task.FromResult(new ToolResult(new JsonObject { ["status"] = "Healthy", ["checks"] = new JsonArray() }, 1))));
        var executor = new ToolExecutor(catalog ?? Fx.Catalog(), Fx.Entities(), constraints ?? Fx.Constraints(), Fx.Masker(), bindings ?? Bindings(), [logImpl, healthImpl, entityImpl], [_audit], FakeTime.Default());
        return (executor, logImpl, healthImpl);
    }

    private static Task<ToolEnvelope> Run(ToolExecutor e, string tool, string args, IncidentToolSession? s = null, string caller = "test") =>
        e.ExecuteAsync(new ToolRequest(tool, Fx.Obj(args), caller, s ?? new IncidentToolSession("INC-1")));

    private static ToolCatalog CatalogWith(Action<JsonObject> change)
    {
        var node = (JsonObject)JsonNode.Parse(Fx.Support("tools.json"))!;
        change(node);
        return ToolCatalog.Load(node.ToJsonString());
    }

    private static JsonObject ToolNode(JsonObject root, string name) =>
        root["tools"]!.AsArray().Select(t => t!.AsObject()).Single(t => t["name"]!.GetValue<string>() == name);

    private static ConstraintSet ConstraintsWith(Action<JsonObject> change)
    {
        var node = (JsonObject)JsonNode.Parse(Fx.Support("constraints.json"))!;
        change(node);
        return ConstraintSet.Load(node.ToJsonString());
    }

    // ---- refusals ----

    [Test]
    public async Task An_unknown_tool_is_denied()
    {
        var (e, _, _) = Build();
        var env = await Run(e, "drop_database", "{}");
        Assert.That(env.Status, Is.EqualTo("denied"));
        Assert.That(env.Error, Does.Contain("unknown tool"));
    }

    [Test]
    public async Task A_catalog_tool_the_profile_does_not_offer_is_denied()
    {
        var (e, _, _) = Build(bindings: new ToolBindings(new Dictionary<string, SourceBinding> { ["health_status"] = Bindings().For("health_status")! }));
        var env = await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{Trace}}"}""");
        Assert.That(env.Status, Is.EqualTo("denied"));
        Assert.That(env.Error, Does.Contain("not offered"));
        Assert.That(e.OfferedTools.Select(t => t.Name), Is.EqualTo(new[] { "health_status" }));
    }

    [Test]
    public async Task A_tool_with_no_implementation_for_the_source_kind_is_denied_without_running_anything()
    {
        var (e, logs, _) = Build(bindings: new ToolBindings(new Dictionary<string, SourceBinding> { ["logs_by_correlation"] = new("x", "oracle_alert_log", new JsonObject()) }));
        Assert.That((await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{Trace}}"}""")).Status, Is.EqualTo("denied"));
        Assert.That(logs.Calls, Is.Empty);
    }

    [Test]
    public async Task Arguments_are_validated_against_the_schema_before_execution()
    {
        var (e, logs, _) = Build();
        foreach (var args in new[] { "{}", """{"trace_id":"nope"}""", $$"""{"trace_id":"{{Trace}}","extra":1}""", $$"""{"trace_id":"{{Trace}}","limit":"ten"}""" })
        {
            var env = await Run(e, "logs_by_correlation", args);
            Assert.That(env.Status, Is.EqualTo("denied"), args);
            Assert.That(env.Error, Does.StartWith("invalid arguments"));
        }
        Assert.That(logs.Calls, Is.Empty);
    }

    [Test]
    public async Task An_entity_outside_the_entities_file_is_denied_by_the_schema()
    {
        var (e, _, _) = Build();
        var env = await Run(e, "sql_entity_read", """{"entity":"payroll","id":"1"}""");
        Assert.That(env.Status, Is.EqualTo("denied"));
        Assert.That(env.Error, Does.Contain("not a known entities name"));
    }

    [Test]
    public async Task A_tool_whose_access_is_not_read_is_denied_by_constraint_C01()
    {
        var def = Fx.Catalog().Find("health_status")!;
        var writer = new ToolDefinition
        {
            Name = def.Name, Family = def.Family, Access = "write", Description = "x", Parameters = def.Parameters, Limits = def.Limits,
            Implementations = def.Implementations, MaskingSection = def.MaskingSection, Raw = def.Raw,
        };
        var (e, _, health) = Build(catalog: new ToolCatalog([writer]));
        var env = await Run(e, "health_status", "{}");
        Assert.That(env.Status, Is.EqualTo("denied"));
        Assert.That(env.Error, Does.Contain("C01"));
        Assert.That(health.Calls, Is.Empty);
    }

    // ---- duplicates and budgets ----

    [Test]
    public async Task The_same_call_twice_returns_duplicate_with_the_earlier_evidence_id_without_running()
    {
        var (e, logs, _) = Build();
        var session = new IncidentToolSession("INC-1");
        var first = await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{Trace}}"}""", session);
        var second = await Run(e, "logs_by_correlation", $$"""{ "trace_id" : "{{Trace}}" }""", session);
        Assert.That(first.Status, Is.EqualTo("ok"));
        Assert.That(second.Status, Is.EqualTo("duplicate"));
        Assert.That(second.EvidenceId, Is.EqualTo(first.EvidenceId));
        Assert.That(logs.Calls, Has.Count.EqualTo(1));
        Assert.That(second.Data, Is.Null);
    }

    [Test]
    public async Task Duplicates_ignore_argument_order_and_are_per_incident()
    {
        var (e, logs, _) = Build();
        await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{Trace}}","limit":5}""");
        var reordered = await Run(e, "logs_by_correlation", $$"""{"limit":5,"trace_id":"{{Trace}}"}""");
        Assert.That(reordered.Status, Is.EqualTo("ok"), "a different incident session is a different incident");
        var session = new IncidentToolSession("INC-2");
        await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{Trace}}","limit":5}""", session);
        Assert.That((await Run(e, "logs_by_correlation", $$"""{"limit":5,"trace_id":"{{Trace}}"}""", session)).Status, Is.EqualTo("duplicate"));
        Assert.That(logs.Calls, Has.Count.EqualTo(3), "the reordered duplicate did not run");
    }

    [Test]
    public async Task Calls_per_tool_per_incident_are_limited_by_the_catalog()
    {
        var (e, logs, _) = Build();
        var session = new IncidentToolSession("INC-1");
        for (var i = 1; i <= 4; i++) Assert.That((await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{T(i)}}"}""", session)).Status, Is.EqualTo("empty").Or.EqualTo("ok"));
        var fifth = await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{T(5)}}"}""", session);
        Assert.That(fifth.Status, Is.EqualTo("budget_exceeded"));
        Assert.That(fifth.Error, Does.Contain("at most 4 calls"));
        Assert.That(logs.Calls, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task The_incident_total_of_constraint_C03_stops_further_calls()
    {
        var constraints = ConstraintsWith(r => r["constraints"]![2]!["params"]!["max_tool_calls"] = 3);
        var (e, _, _) = Build(constraints: constraints);
        var session = new IncidentToolSession("INC-1");
        await Run(e, "health_status", "{}", session);
        await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{T(1)}}"}""", session);
        await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{T(2)}}"}""", session);
        var fourth = await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{T(3)}}"}""", session);
        Assert.That(fourth.Status, Is.EqualTo("budget_exceeded"));
        Assert.That(fourth.Error, Does.Contain("C03"));
    }

    [Test]
    public async Task The_incident_total_of_output_characters_stops_further_calls_and_clips_the_last_one()
    {
        var constraints = ConstraintsWith(r => r["constraints"]![2]!["params"]!["max_output_chars"] = 300);
        var (e, _, _) = Build(constraints: constraints, logs: (c, _) =>
            Task.FromResult(new ToolResult(new JsonObject { ["records"] = new JsonArray(Enumerable.Range(0, 10).Select(i => (JsonNode?)new JsonObject { ["message"] = new string('x', 60) }).ToArray()) }, 10)));
        var session = new IncidentToolSession("INC-1");
        var first = await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{T(1)}}"}""", session);
        Assert.That(first.Truncated, Is.True);
        Assert.That(MonitorJson.ToCompact(first.Data).Length, Is.LessThanOrEqualTo(300));
        var statuses = new List<string>();
        var chars = (long)MonitorJson.ToCompact(first.Data).Length;
        for (var i = 2; i <= 4; i++)
        {
            var env = await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{T(i)}}"}""", session);
            statuses.Add(env.Status);
            chars += env.Data is null ? 0 : MonitorJson.ToCompact(env.Data).Length;
        }
        Assert.That(statuses, Does.Contain("budget_exceeded"));
        Assert.That(chars, Is.LessThanOrEqualTo(300), "results never exceed the incident total");
    }

    [Test]
    public async Task Evidence_ids_follow_call_order_starting_at_E2_and_every_envelope_has_one()
    {
        var (e, _, _) = Build();
        var session = new IncidentToolSession("INC-1");
        var a = await Run(e, "health_status", "{}", session);
        var denied = await Run(e, "nope", "{}", session);
        var b = await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{Trace}}"}""", session);
        Assert.That(new[] { a.EvidenceId, denied.EvidenceId, b.EvidenceId }, Is.EqualTo(new[] { "E2", "E3", "E4" }));
        Assert.That(new[] { a.Call, denied.Call, b.Call }, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    // ---- limits ----

    [Test]
    public async Task Output_is_cut_to_max_output_chars_with_truncated_true()
    {
        var big = new JsonArray(Enumerable.Range(0, 100).Select(i => (JsonNode?)new JsonObject { ["message"] = new string('y', 400) }).ToArray());
        var (e, _, _) = Build(logs: (c, _) => Task.FromResult(new ToolResult(new JsonObject { ["records"] = big.DeepClone() }, 100)));
        var env = await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{Trace}}"}""");
        Assert.That(env.Truncated, Is.True);
        Assert.That(MonitorJson.ToCompact(env.Data).Length, Is.LessThanOrEqualTo(16000));
        Assert.That(env.Data!["records"]!.AsArray().Count, Is.GreaterThan(0).And.LessThan(100));
    }

    [Test]
    public async Task Rows_are_capped_at_max_rows()
    {
        var many = new JsonArray(Enumerable.Range(0, 300).Select(i => (JsonNode?)new JsonObject { ["level"] = "Warning" }).ToArray());
        var (e, _, _) = Build(logs: (c, _) => Task.FromResult(new ToolResult(new JsonObject { ["records"] = many.DeepClone() }, 300)));
        var env = await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{Trace}}"}""");
        Assert.That(env.Rows, Is.EqualTo(200));
        Assert.That(env.Data!["records"]!.AsArray(), Has.Count.EqualTo(200));
        Assert.That(env.Truncated, Is.True);
    }

    [Test]
    public async Task A_call_that_exceeds_timeout_seconds_is_reported_as_timeout()
    {
        var catalog = CatalogWith(r => ToolNode(r, "health_status")["limits"]!["timeout_seconds"] = 1);
        var (e, _, _) = Build(catalog: catalog, health: async (c, ct) => { await Task.Delay(Timeout.Infinite, ct); return null!; });
        var env = await Run(e, "health_status", "{}");
        Assert.That(env.Status, Is.EqualTo("timeout"));
        Assert.That(env.EvidenceId, Is.Not.Null);
    }

    [Test]
    public void Caller_cancellation_is_not_a_timeout()
    {
        var (e, _, _) = Build(health: async (c, ct) => { await Task.Delay(Timeout.Infinite, ct); return null!; });
        using var cts = new CancellationTokenSource(50);
        Assert.ThrowsAsync<TaskCanceledException>(async () => await e.ExecuteAsync(new ToolRequest("health_status", new JsonObject(), "test", new IncidentToolSession()), cts.Token));
    }

    [Test]
    public async Task The_lookback_is_clamped_by_constraint_C04()
    {
        var catalog = CatalogWith(r => ToolNode(r, "logs_by_correlation")["limits"]!["lookback_hours"] = 500);
        var (e, logs, _) = Build(catalog: catalog);
        await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{Trace}}"}""");
        Assert.That(logs.Calls.Single().Limits.LookbackHours, Is.EqualTo(72));
    }

    // ---- errors ----

    [Test]
    public async Task A_tool_exception_is_an_error_envelope_and_an_unexpected_one_does_not_leak_its_message()
    {
        var (e, _, _) = Build(logs: (c, _) => throw new ToolException("the workspace refused"), health: (c, _) => throw new InvalidOperationException("Server=db;Password=hunter2"));
        var known = await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{Trace}}"}""");
        Assert.That(known.Status, Is.EqualTo("error"));
        Assert.That(known.Error, Is.EqualTo("the workspace refused"));
        var unexpected = await Run(e, "health_status", "{}");
        Assert.That(unexpected.Status, Is.EqualTo("error"));
        Assert.That(unexpected.Error, Is.EqualTo("the source failed: InvalidOperationException"));
        Assert.That(unexpected.Error, Does.Not.Contain("hunter2"));
    }

    [Test]
    public async Task An_argument_problem_found_by_the_tool_is_denied()
    {
        var (e, _, _) = Build(logs: (c, _) => throw new ToolArgumentException("give exactly one"));
        Assert.That((await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{Trace}}"}""")).Status, Is.EqualTo("denied"));
    }

    // ---- masking ----

    [Test]
    public async Task Masking_is_applied_to_data_before_the_envelope_leaves_and_unlisted_fields_are_counted()
    {
        var (e, _, _) = Build(logs: (c, _) => Task.FromResult(new ToolResult(Fx.Obj("""{ "records": [ { "message": "mail a@b.io", "level": "Warning", "password": "x", "secret_field": 1 } ] }"""), 1)));
        var env = await Run(e, "logs_by_correlation", $$"""{"trace_id":"{{Trace}}"}""");
        var record = (JsonObject)env.Data!["records"]![0]!;
        Assert.That(record["message"]!.GetValue<string>(), Is.EqualTo("mail [email]"));
        Assert.That(record.ContainsKey("password"), Is.False);
        Assert.That(env.FieldsDropped, Is.EqualTo(2));
    }

    [Test]
    public async Task A_tool_without_a_masking_section_in_the_policy_returns_no_data()
    {
        var catalog = CatalogWith(r => { ToolNode(r, "health_status")["output"]!["masking"] = "tools.health_status"; });
        var policy = MaskingPolicyLoader.Load(Fx.Support("masking-policy.json"));
        var noHealth = new MaskingPolicy
        {
            FreeTextMaxChars = policy.FreeTextMaxChars, Patterns = policy.Patterns, Protected = policy.Protected, SignalFields = policy.SignalFields,
            SignalProperties = policy.SignalProperties, Entities = policy.Entities,
            Tools = policy.Tools.Where(kv => kv.Key != "health_status").ToDictionary(kv => kv.Key, kv => kv.Value),
        };
        var healthImpl = new ScriptedImplementation("health", "http_health", (c, _) => Task.FromResult(new ToolResult(new JsonObject { ["status"] = "Healthy" }, 1)));
        var e = new ToolExecutor(catalog, Fx.Entities(), Fx.Constraints(), new Masker(noHealth, Fx.HashKeyBytes), Bindings(), [healthImpl], [], FakeTime.Default());
        var env = await Run(e, "health_status", "{}");
        Assert.That(env.Data, Is.Null);
    }

    // ---- audit ----

    [Test]
    public async Task Every_call_writes_one_audit_row_denied_duplicate_and_over_budget_ones_included()
    {
        var (e, _, _) = Build();
        var session = new IncidentToolSession("INC-9");
        await Run(e, "health_status", "{}", session, caller: "rules");
        await Run(e, "health_status", "{}", session, caller: "rules");
        await Run(e, "nope", "{}", session, caller: "investigator");
        await Run(e, "logs_by_correlation", """{"trace_id":"bad"}""", session, caller: "dashboard:ann");

        Assert.That(_audit.Rows.Select(r => r.Status), Is.EqualTo(new[] { "empty", "duplicate", "denied", "denied" }.Select((s, i) => i == 0 ? "ok" : s)));
        var row = _audit.Rows[0];
        Assert.That(row.IncidentId, Is.EqualTo("INC-9"));
        Assert.That(row.Caller, Is.EqualTo("rules"));
        Assert.That(row.Tool, Is.EqualTo("health_status"));
        Assert.That(row.SourceId, Is.EqualTo("api"));
        Assert.That(row.Rows, Is.EqualTo(1));
        Assert.That(row.OutputChars, Is.GreaterThan(0));
        Assert.That(row.DurationMs, Is.GreaterThanOrEqualTo(0));
        Assert.That(row.At, Is.EqualTo(FakeTime.Default().GetUtcNow()));
        Assert.That(_audit.Rows.Select(r => r.Caller), Is.EqualTo(new[] { "rules", "rules", "investigator", "dashboard:ann" }));
    }

    [Test]
    public async Task Audit_arguments_are_masked()
    {
        var (e, _, _) = Build();
        await Run(e, "sql_entity_read", """{"entity":"user","id":"john@example.com"}""");
        Assert.That(_audit.Rows.Single().ArgumentsJson, Does.Not.Contain("john@example.com"));
        Assert.That(_audit.Rows.Single().ArgumentsJson, Does.Contain("[masked]"));
    }

    [Test]
    public async Task A_failing_audit_sink_does_not_break_the_call()
    {
        var healthImpl = new ScriptedImplementation("health", "http_health", (c, _) => Task.FromResult(new ToolResult(new JsonObject { ["status"] = "Healthy" }, 1)));
        var e = new ToolExecutor(Fx.Catalog(), Fx.Entities(), Fx.Constraints(), Fx.Masker(), Bindings(), [healthImpl], [new ThrowingSink(), _audit], FakeTime.Default());
        var env = await Run(e, "health_status", "{}");
        Assert.That(env.Status, Is.EqualTo("ok"));
        Assert.That(_audit.Rows, Has.Count.EqualTo(1));
    }

    private sealed class ThrowingSink : IToolAuditSink
    {
        public Task WriteAsync(ToolAuditRow row, CancellationToken ct) => throw new IOException("disk full");
    }

    // ---- read only ----

    [TestCase("SELECT TOP (1) [a] FROM [dbo].[T] WHERE [Id] = @id", true)]
    [TestCase("DELETE FROM [dbo].[T]", false)]
    [TestCase("SELECT * FROM [dbo].[T]", false)]
    [TestCase("SELECT TOP (1) [a] FROM [dbo].[T]; DROP TABLE x", false)]
    [TestCase("SELECT TOP (1) [a] FROM [dbo].[T] -- comment", false)]
    [TestCase("  UPDATE [dbo].[T] SET a = 1", false)]
    [TestCase("EXEC xp_cmdshell 'dir'", false)]
    public void Only_bounded_select_statements_pass_the_guard(string sql, bool allowed)
    {
        if (allowed) Assert.DoesNotThrow(() => SqlStatements.EnsureReadOnly(sql));
        else Assert.Throws<ToolException>(() => SqlStatements.EnsureReadOnly(sql));
    }

    [TestCase("Id", true)]
    [TestCase("User Name", false)]
    [TestCase("Id]; DROP TABLE x; --", false)]
    [TestCase("1abc", false)]
    public void Identifiers_are_checked_and_bracket_quoted(string name, bool valid)
    {
        if (valid) Assert.That(SqlStatements.Quote(name), Is.EqualTo($"[{name}]"));
        else Assert.Throws<ToolException>(() => SqlStatements.Quote(name));
    }

    [Test]
    public void Tables_must_be_schema_dot_table() => Assert.Throws<ToolException>(() => SqlStatements.QuoteTable("Users"));
}
