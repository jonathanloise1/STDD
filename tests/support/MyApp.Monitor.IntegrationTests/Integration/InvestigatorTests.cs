using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Investigator;
using MyApp.Monitor.Store;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Tools;
using NUnit.Framework;
using static MyApp.Monitor.Tests.ScriptedBackend;

namespace MyApp.Monitor.Tests.Integration;

/// <summary>
/// The investigator host (operations.md section 11) against a scripted model host: the loop of 11.3 with its stop rules, validation and repair, masking, the
/// verdicts stored side by side with the rule verdict, failure handling, and the background queue. No model, no network.
/// </summary>
public class InvestigatorTests
{
    private const string Planted = "jane.doe@example.com";
    private Lab _lab = null!;

    [SetUp]
    public void SetUp() => _lab = new Lab();

    [TearDown]
    public void TearDown() => _lab.Dispose();

    private static string TextOf(GenerationRequest r) => string.Join("\n", r.Messages.Select(m => m.Content));
    private static string Last(GenerationRequest r) => r.Messages[^1].Content;
    private static bool OnlyReport(GenerationRequest r) => r.Grammar is not null && !r.Grammar.Contains("<call>", StringComparison.Ordinal);

    private static SchemaSubsetValidator Schema() => new(JsonNode.Parse(Fx.Support("incident-report.schema.json"))!);

    // ---- the full loop ----

    [Test]
    public async Task A_full_investigation_stores_the_model_verdict_next_to_the_rule_verdict_and_fills_the_model_block()
    {
        _lab.WriteLog($"Task not created for {Planted}");
        var id = await _lab.KeyedIncidentAsync();
        _lab.Small
            .Say(Call("logs_by_correlation", $$"""{"trace_id":"{{Rig.Trace}}"}"""))
            .Say(Call("health_status"))
            .Say(Report(evidence: ["E3", "E4"], cause: "The database did not answer when the task was saved.", story: "US-TASK-01", area: "tasks"));

        await _lab.InvestigateAsync(id);

        var row = (await _lab.Rig.Store.GetAsync(id))!;
        var report = row.Report;
        Assert.That(Schema().IsValid(report, out var errors), Is.True, string.Join("; ", errors));
        Assert.That(report["rule_verdict"]!["cause_category"]!.GetValue<string>(), Is.EqualTo("dependency_unavailable"), "the rule verdict stays");
        Assert.That(report["model_verdict"]!["cause"]!.GetValue<string>(), Does.StartWith("The database did not answer"));
        Assert.That(report["model_verdict"]!["evidence_refs"]!.AsArray().Select(x => x!.GetValue<string>()), Is.EqualTo(new[] { "E3", "E4" }));
        Assert.That(row.VerdictSource, Is.EqualTo("model"));
        Assert.That(report["investigation"]!["verdict"]!.GetValue<string>(), Is.EqualTo("explained"));
        Assert.That(report["model"]!.ToJsonString(), Is.EqualTo("""{"model_id":"investigator","version":"1.0.0","tier":"small","device":"cpu","tool_calls":2,"abstained":false}"""));
        Assert.That(report["pack_version"]!.GetValue<string>(), Is.EqualTo(_lab.Pack.Version));

        // the evidence the model cited is in the report, numbered after the rule engine's, each with a reference a person can open
        var evidence = report["evidence"]!.AsArray().Select(e => (Id: e!["id"]!.GetValue<string>(), Source: e["source"]!.GetValue<string>(), Reference: e["reference"]!.GetValue<string>())).ToList();
        Assert.That(evidence.Select(e => e.Id), Is.EqualTo(new[] { "E1", "E2", "E3", "E4" }));
        Assert.That(evidence[2], Is.EqualTo(("E3", "logs_by_correlation", $"logs_by_correlation trace_id={Rig.Trace}")));
        Assert.That(evidence[3].Source, Is.EqualTo("health_status"));

        var actions = report["actions_taken"]!.AsArray().Select(a => a!["action"]!.GetValue<string>()).ToList();
        Assert.That(actions, Does.Contain("investigation_done"));
        Assert.That(report["actions_taken"]!.AsArray().Last(a => a!["action"]!.GetValue<string>() == "investigation_done")!["reference"]!.GetValue<string>(),
            Is.EqualTo("tier small, 2 tool call(s): logs_by_correlation, health_status"));
        Assert.That((await _lab.Rig.Store.GetInvestigationAsync(id))!.Status, Is.EqualTo("done"));
        var audit = (await _lab.Rig.Store.ToolAuditAsync(id)).Where(a => a.Caller == "investigator").ToList();
        Assert.That(audit.Select(a => a.Tool).Reverse(), Is.EqualTo(new[] { "logs_by_correlation", "health_status" }), "every call is audited under the investigator caller");
        var events = await _lab.Rig.Store.EventsAsync(id);
        Assert.That(events.Single(e => e.Kind == "investigation").DataJson, Does.Contain("\"tools\":[\"logs_by_correlation\",\"health_status\"]"));
    }

    [Test]
    public async Task The_prompt_is_the_protocol_rendered_from_the_incident_the_pack_and_the_profile()
    {
        var id = await _lab.KeyedIncidentAsync();
        _lab.Small.Say(Report(verdict: "abstain", abstainReason: "insufficient_evidence", cause: null, confidence: 0.2, category: "unknown"));
        await _lab.InvestigateAsync(id);

        var first = _lab.Small.Requests.Single();
        Assert.That(first.Messages.Select(m => m.Role), Is.EqualTo(new[] { "system", "user" }));
        Assert.That(first.Messages[0].Content, Is.EqualTo(ProtocolBlock.SystemText(_lab.Pack.ProtocolText)));
        var user = first.Messages[1].Content;
        Assert.That(user, Does.StartWith("<incident>{\"trigger\":\"severity\",\"severity\":\"Error\",\"environment\":\"prod\",\"signature\":{\"kind\":\"keyed\",\"id\":\"task.create.unavailable\""));
        Assert.That(user, Does.Contain("\"evidence\":[{\"id\":\"E1\",\"source\":\"signal\",\"records\":[{\"Timestamp\":").And.Contain("\"LogKey\":\"task.create.unavailable\""));
        Assert.That(user, Does.Contain("<pack>{\"signature\":{\"id\":\"task.create.unavailable\"").And.Contain("\"runbook\":\"### Meaning").And.Not.Contain("call_site"));
        Assert.That(user, Does.Contain("<tools>{\"tools\":[{\"name\":\"audit_trail_by_entity\"").And.Contain("\"entities\":[{\"name\":\"membership\"").And.EndWith("<budget calls=\"6\" />"));
        Assert.That(user, Does.Contain("<constraints>\nC01: ").And.Contain("</constraints>\n<budget"));
        Assert.That(first.Grammar, Does.StartWith("root ::= ").And.Contain("<call>").And.Contain("<report>"));
        Assert.That(TurnGrammar.UndefinedRules(first.Grammar!), Is.Empty);
        Assert.That(first.MaxTokens, Is.EqualTo(700));
    }

    [TestCase("user", "user")]
    [TestCase(null, "tool")]
    public async Task The_results_travel_with_the_tool_role_of_the_lock(string? toolRole, string expectedRole)
    {
        using var lab = new Lab(lockText: Lab.LockText(SharedFixtures.Pack, toolRole: toolRole));
        var id = await lab.KeyedIncidentAsync();
        lab.Small.Say(Call("health_status")).Say(Report(verdict: "abstain", abstainReason: "insufficient_evidence", cause: null, confidence: 0.2, category: "unknown"));
        await lab.InvestigateAsync(id);

        var second = lab.Small.Requests[1];
        Assert.That(second.Messages.Select(m => m.Role), Is.EqualTo(new[] { "system", "user", "assistant", expectedRole }));
        Assert.That(second.Messages[3].Content, Does.StartWith("<result>").And.Contain("</result>\n<budget calls=\""));
    }

    [Test]
    public async Task A_failed_rule_check_is_told_to_the_model_next_to_the_trigger()
    {
        using var repo = new FixtureRepo();
        repo.Replace(FixtureRepo.Ops,
            "The task could not be saved because a dependency failed. The user was asked to retry.\n\n### Check\n\n1. Read the health of the application with `health_status`.\n\n### Rule checks\n\nNone.",
            "The task could not be saved because a dependency failed. The user was asked to retry.\n\n### Check\n\n1. Read the health of the application with `health_status`.\n\n### Rule checks\n\n| Tool | Arguments | Expect |\n|------|-----------|--------|\n| health_status | - | status == \"Healthy\" |");
        using var lab = new Lab(repo.BuildAndLoad(), health: """{"status":"Degraded","checks":[{"name":"sqlserver","status":"Degraded","durationMs":900}]}""");
        var id = await lab.KeyedIncidentAsync();
        Assert.That((await lab.ReportAsync(id))["runbook"]!["checks"]![0]!["passed"]!.GetValue<bool>(), Is.False);
        lab.Small.Say(Report(verdict: "abstain", abstainReason: "conflicting_evidence", cause: null, confidence: 0.2, category: "unknown"));
        await lab.InvestigateAsync(id);
        var user = lab.Small.Requests.Single().Messages[1].Content;
        Assert.That(user, Does.Contain("\"rule_checks\":[{\"tool\":\"health_status\",\"expect\":\"status == \\\"Healthy\\\"\",\"passed\":false}]"));
        Assert.That(user, Does.StartWith("<incident>{\"trigger\":\"severity\""), "the trigger stays a value of the report schema");
    }

    // ---- masking ----

    [Test]
    public async Task Everything_the_model_sees_is_masked_an_address_planted_in_a_tool_result_never_reaches_the_prompt()
    {
        _lab.WriteLog($"Task not created for {Planted}", $"Please call back {Planted} today");
        var id = await _lab.KeyedIncidentAsync();
        _lab.Small
            .Say(Call("logs_by_correlation", $$"""{"trace_id":"{{Rig.Trace}}"}"""))
            .Say(Report(evidence: ["E3"], cause: $"The user {Planted} could not save; the database was down."));
        await _lab.InvestigateAsync(id);

        var second = _lab.Small.Requests[1];
        var result = second.Messages.Single(m => m.Role == "tool").Content;
        Assert.That(result, Does.Contain("[email]").And.Not.Contain(Planted));
        Assert.That(string.Join("\n", _lab.Small.Requests.Select(TextOf)), Does.Not.Contain(Planted), "no prompt of the whole investigation holds the address");
        var report = await _lab.ReportAsync(id);
        Assert.That(report.ToJsonString(), Does.Not.Contain(Planted), "nor the stored report, even when the model wrote it");
        Assert.That(report["model_verdict"]!["cause"]!.GetValue<string>(), Does.Contain("[email]"));
    }

    // ---- stop rules ----

    private static OperationsPack WithBudget(int calls)
    {
        using var repo = new FixtureRepo();
        repo.Replace("docs/support/profiles/onprem.yaml", "max_tool_calls: 6", $"max_tool_calls: {calls}");
        return repo.BuildAndLoad();
    }

    [Test]
    public async Task When_the_calls_left_reach_zero_the_next_turn_is_constrained_to_the_report()
    {
        using var lab = new Lab(WithBudget(2));
        var id = await lab.KeyedIncidentAsync();
        lab.Small.Say(Call("health_status")).Say(Call("logs_by_correlation", $$"""{"trace_id":"{{Rig.Trace}}"}""")).Say(Report(verdict: "abstain", abstainReason: "budget_exhausted", cause: null, confidence: 0.3, category: "unknown"));
        await lab.InvestigateAsync(id);

        Assert.That(lab.Small.Requests.Select(OnlyReport), Is.EqualTo(new[] { false, false, true }));
        Assert.That(Last(lab.Small.Requests[2]), Does.EndWith("<budget calls=\"0\" />"));
        Assert.That(Last(lab.Small.Requests[1]), Does.EndWith("<budget calls=\"1\" />"));
        var report = await lab.ReportAsync(id);
        Assert.That(report["investigation"]!["abstain_reason"]!.GetValue<string>(), Is.EqualTo("budget_exhausted"));
        Assert.That(report["model_verdict"], Is.Null, "an abstention asserts no cause");
        Assert.That(report["model"]!["abstained"]!.GetValue<bool>(), Is.True);
        Assert.That(report["rule_verdict"], Is.Not.Null);
        Assert.That((await lab.Rig.Store.GetInvestigationAsync(id))!.Status, Is.EqualTo("abstained"));
        Assert.That(report["actions_taken"]!.AsArray().Select(a => a!["action"]!.GetValue<string>()), Does.Contain("investigation_abstained"));
    }

    [Test]
    public async Task The_same_call_twice_returns_duplicate_and_a_second_duplicate_forces_the_report()
    {
        var id = await _lab.KeyedIncidentAsync();
        _lab.Small.Say(Call("health_status")).Say(Call("health_status")).Say(Call("health_status")).Say(Report(evidence: ["E3"]));
        await _lab.InvestigateAsync(id);

        var results = _lab.Small.Requests.Select(r => r.Messages.Where(m => m.Role == "tool").Select(m => m.Content).ToList()).ToList();
        Assert.That(results[2][1], Does.Contain("\"status\":\"duplicate\"").And.Contain("\"evidence_id\":\"E3\""), "the duplicate points at the earlier evidence");
        Assert.That(_lab.Small.Requests.Select(OnlyReport), Is.EqualTo(new[] { false, false, false, true }), "one duplicate is tolerated, the second ends the calls");
        Assert.That((await _lab.ReportAsync(id))["model"]!["tool_calls"]!.GetValue<int>(), Is.EqualTo(3));
    }

    [Test]
    public async Task Two_results_in_a_row_that_failed_force_the_report_and_a_success_in_between_resets_the_count()
    {
        var id = await _lab.KeyedIncidentAsync();
        _lab.Small
            .Say(Call("logs_by_correlation", """{"trace_id":"short"}"""))      // denied
            .Say(Call("health_status"))                                         // ok: the count restarts
            .Say(Call("logs_by_correlation", """{"trace_id":"also short"}"""))  // denied
            .Say(Call("sql_entity_read", """{"entity":"nope","id":"1"}"""))     // denied: two in a row
            .Say(Report(verdict: "abstain", abstainReason: "insufficient_evidence", cause: null, confidence: 0.3, category: "unknown"));
        await _lab.InvestigateAsync(id);
        Assert.That(_lab.Small.Requests.Select(OnlyReport), Is.EqualTo(new[] { false, false, false, false, true }));
        Assert.That(TextOf(_lab.Small.Requests[1]), Does.Contain("\"status\":\"denied\""));
    }

    [Test]
    public async Task A_call_the_executor_denies_never_reaches_a_source_and_is_audited_with_masked_arguments()
    {
        var id = await _lab.KeyedIncidentAsync();
        _lab.Small.Say(Call("sql_entity_read", """{"entity":"user","id":"1; DROP TABLE Users"}""")).Say(Report(verdict: "abstain", abstainReason: "insufficient_evidence", cause: null, confidence: 0.2, category: "unknown"));
        await _lab.InvestigateAsync(id);
        var audit = (await _lab.Rig.Store.ToolAuditAsync(id)).Single(a => a.Caller == "investigator");
        Assert.That(audit.Status, Is.EqualTo("denied"));
        Assert.That(_lab.Rig.Sql.Statements, Is.Empty.Or.None.Contain("DROP"), "nothing the model sent became SQL");
    }

    [Test]
    public async Task The_per_tool_budget_of_the_executor_is_reported_as_budget_exceeded()
    {
        var id = await _lab.KeyedIncidentAsync();
        // logs_by_correlation allows a few calls per incident; the model keeps asking with a new limit each time
        var catalog = _lab.Pack.Tools.Find("logs_by_correlation")!.Limits.MaxCallsPerIncident;
        for (var i = 0; i <= catalog; i++) _lab.Small.Say(Call("logs_by_correlation", $$"""{"trace_id":"{{Rig.Trace}}","limit":{{10 + i}}}"""));
        _lab.Small.Say(Report(verdict: "abstain", abstainReason: "budget_exhausted", cause: null, confidence: 0.2, category: "unknown"));
        await _lab.InvestigateAsync(id);
        Assert.That(TextOf(_lab.Small.Requests[^1]), Does.Contain("\"status\":\"budget_exceeded\""));
    }

    [Test]
    public async Task The_wall_time_stops_the_investigation_as_failed_and_the_rule_verdict_stays()
    {
        var id = await _lab.KeyedIncidentAsync();
        _lab.Small.Before = _ => { _lab.Rig.Time.Advance(TimeSpan.FromMinutes(11)); return Task.CompletedTask; };   // the host is slow: the profile allows 600 seconds
        _lab.Small.Say(Call("health_status")).Say(Report(evidence: ["E3"]));
        await _lab.InvestigateAsync(id);

        var row = (await _lab.Rig.Store.GetAsync(id))!;
        Assert.That(row.Report["model_verdict"], Is.Null);
        Assert.That(row.Report["rule_verdict"], Is.Not.Null);
        Assert.That(row.Report["model"], Is.Null);
        Assert.That(row.VerdictSource, Is.EqualTo("rule"));
        var inv = (await _lab.Rig.Store.GetInvestigationAsync(id))!;
        Assert.That((inv.Status, inv.Error), Is.EqualTo(("failed", "wall time reached")));
        Assert.That(row.Report["actions_taken"]!.AsArray().Last()!["action"]!.GetValue<string>(), Is.EqualTo("investigation_failed"));
        Assert.That(Schema().IsValid(row.Report, out _), Is.True);
    }

    [Test]
    public void The_oldest_results_are_replaced_by_their_summary_above_75_percent_of_the_context()
    {
        string Result(string id, string payload) => $"<result>{{\"tool\":\"t\",\"call\":1,\"status\":\"ok\",\"evidence_id\":\"{id}\",\"rows\":3,\"data\":{{\"x\":\"{payload}\"}}}}</result>\n<budget calls=\"4\" />";
        var messages = new List<ChatMessage>
        {
            new("system", "rules"), new("user", "incident"), new("assistant", "<call>{}</call>"), new("tool", Result("E2", new string('a', 3000))),
            new("assistant", "<call>{}</call>"), new("tool", Result("E3", new string('b', 3000))), new("assistant", "<call>{}</call>"), new("tool", Result("E4", "small")),
        };
        var view = InvestigationLoop.SummariseOldResults(messages, "tool", 1000, 0.75);
        Assert.That(view[3].Content, Is.EqualTo("<result>{\"evidence_id\":\"E2\",\"summarised\":true,\"rows\":3}</result>\n<budget calls=\"4\" />"));
        Assert.That(view[5].Content, Does.Contain("summarised"), "still above the limit: the next oldest goes too");
        Assert.That(view[7].Content, Is.EqualTo(messages[7].Content), "the newest stays");
        Assert.That(messages[3].Content, Does.Contain("aaaa"), "the conversation itself is not changed");
        Assert.That(InvestigationLoop.SummariseOldResults(messages, "tool", 100_000, 0.75).Select(m => m.Content), Is.EqualTo(messages.Select(m => m.Content)));
    }

    // ---- the report: validation, repair, abstention ----

    [Test]
    public async Task An_invalid_report_gets_one_repair_turn_with_the_error_and_a_valid_second_report_is_stored()
    {
        var id = await _lab.KeyedIncidentAsync();
        _lab.Small.Say(Report(evidence: ["E9"])).Say(Report(evidence: ["E1"]));
        await _lab.InvestigateAsync(id);

        var repair = _lab.Small.Requests[1];
        Assert.That(Last(repair), Does.StartWith("<validation_error>").And.Contain("E9 was not received"));
        Assert.That(OnlyReport(repair), Is.True, "the repair turn is constrained to the report");
        var report = await _lab.ReportAsync(id);
        Assert.That(report["model_verdict"]!["evidence_refs"]![0]!.GetValue<string>(), Is.EqualTo("E1"));
        Assert.That(report["model"]!["tool_calls"]!.GetValue<int>(), Is.EqualTo(0));
    }

    [Test]
    public async Task A_second_invalid_report_fails_the_investigation_and_leaves_the_incident_with_its_rule_verdict()
    {
        var id = await _lab.KeyedIncidentAsync();
        _lab.Small.Say(Report(evidence: ["E9"])).Say("not a turn at all");
        await _lab.InvestigateAsync(id);

        var inv = (await _lab.Rig.Store.GetInvestigationAsync(id))!;
        Assert.That(inv.Status, Is.EqualTo("failed"));
        Assert.That(inv.Error, Does.StartWith("invalid report after the repair turn"));
        var report = await _lab.ReportAsync(id);
        Assert.That((report["model_verdict"], report["investigation"], report["model"]), Is.EqualTo(((JsonNode?)null, (JsonNode?)null, (JsonNode?)null)));
        Assert.That(report["rule_verdict"], Is.Not.Null);
    }

    [Test]
    public async Task A_call_when_only_the_report_is_allowed_counts_as_an_invalid_report()
    {
        using var lab = new Lab(WithBudget(1));
        var id = await lab.KeyedIncidentAsync();
        lab.Small.Say(Call("health_status")).Say(Call("health_status")).Say(Report(evidence: ["E3"]));
        await lab.InvestigateAsync(id);
        Assert.That(Last(lab.Small.Requests[2]), Does.Contain("the next turn must be the report"));
        Assert.That((await lab.ReportAsync(id))["model_verdict"], Is.Not.Null);
    }

    [Test]
    public async Task An_explained_report_below_half_confidence_is_rewritten_to_abstain()
    {
        var id = await _lab.KeyedIncidentAsync();
        _lab.Small.Say(Report(confidence: 0.4, evidence: ["E1"]));
        await _lab.InvestigateAsync(id);
        var report = await _lab.ReportAsync(id);
        Assert.That(report["investigation"]!["verdict"]!.GetValue<string>(), Is.EqualTo("abstain"));
        Assert.That(report["investigation"]!["abstain_reason"]!.GetValue<string>(), Is.EqualTo("insufficient_evidence"));
        Assert.That(report["model_verdict"], Is.Null);
        Assert.That((await _lab.Rig.Store.GetInvestigationAsync(id))!.Status, Is.EqualTo("abstained"));
    }

    [Test]
    public async Task A_user_story_the_pack_does_not_have_is_refused_and_one_it_has_is_accepted()
    {
        var id = await _lab.KeyedIncidentAsync();
        _lab.Small.Say(Report(evidence: ["E1"], story: "US-NOPE-99")).Say(Report(evidence: ["E1"], story: "US-TASK-01", area: "tasks"));
        await _lab.InvestigateAsync(id);
        Assert.That(Last(_lab.Small.Requests[1]), Does.Contain("US-NOPE-99 is not in the pack"));
        Assert.That((await _lab.ReportAsync(id))["investigation"]!["user_story"]!.GetValue<string>(), Is.EqualTo("US-TASK-01"));
    }

    [Test]
    public async Task The_reproduction_of_the_model_fills_the_report_only_when_the_rule_data_had_none()
    {
        var id = await _lab.KeyedIncidentAsync();
        const string repro = """{"preconditions":["An editor has the form open."],"steps":["An admin removes the editor.","The editor saves."],"expected":"The task is saved.","observed":"The service did not answer."}""";
        _lab.Small.Say(Report(evidence: ["E1"], reproduction: repro));
        await _lab.InvestigateAsync(id);
        Assert.That((await _lab.ReportAsync(id))["reproduction"]!["steps"]!.AsArray().Count, Is.EqualTo(2));
    }

    // ---- failures never block ----

    [Test]
    public async Task A_host_that_fails_mid_investigation_fails_it_and_keeps_the_rule_verdict()
    {
        var id = await _lab.KeyedIncidentAsync();
        _lab.Small.Say(Call("health_status")).Fail(new HostException("the host answered 500"));
        await _lab.InvestigateAsync(id);
        var inv = (await _lab.Rig.Store.GetInvestigationAsync(id))!;
        Assert.That((inv.Status, inv.Error), Is.EqualTo(("failed", "the host answered 500")));
        Assert.That((await _lab.ReportAsync(id))["rule_verdict"], Is.Not.Null);
        Assert.That(_lab.Host.IsAvailable, Is.True, "one failure does not switch the host off");
    }

    [Test]
    public async Task Three_failures_in_a_row_switch_the_host_to_rules_only_until_restart()
    {
        var id = await _lab.KeyedIncidentAsync();
        for (var i = 0; i < 3; i++) _lab.Small.Fail(new HostException("down"));
        for (var i = 0; i < 3; i++) await _lab.InvestigateAsync(id);

        Assert.That(_lab.Host.IsAvailable, Is.False);
        Assert.That(_lab.Host.Status.State, Is.EqualTo(InvestigatorStates.Disabled));
        Assert.That(_lab.Host.Status.Reason, Does.Contain("until the monitor restarts"));
        _lab.Small.Say(Report(evidence: ["E1"]));
        await _lab.InvestigateAsync(id);
        Assert.That((await _lab.Rig.Store.GetInvestigationAsync(id))!.Status, Is.EqualTo("skipped"));
        Assert.That(_lab.Small.Requests, Has.Count.EqualTo(3), "a disabled host asks nothing");

        var restarted = _lab.NewHost();
        await restarted.RefreshAsync(CancellationToken.None);
        Assert.That(restarted.IsAvailable, Is.True, "a restart starts again");
    }

    [Test]
    public async Task A_success_resets_the_count_of_failures_in_a_row()
    {
        var id = await _lab.KeyedIncidentAsync();
        _lab.Small.Fail(new HostException("down")).Fail(new HostException("down")).Say(Report(evidence: ["E1"])).Fail(new HostException("down")).Fail(new HostException("down"));
        for (var i = 0; i < 5; i++) await _lab.InvestigateAsync(id);
        Assert.That(_lab.Host.IsAvailable, Is.True);
        Assert.That((_lab.Host.Status.Completed, _lab.Host.Status.Failed), Is.EqualTo((1, 4)));
    }

    [Test]
    public async Task A_model_report_that_would_break_the_incident_schema_never_changes_the_incident()
    {
        var id = await _lab.KeyedIncidentAsync();
        // the area passes the investigation schema but the whole report must still validate: the story pattern allows what the report's area pattern does not
        _lab.Small.Say(Report(evidence: ["E1"], area: "Tasks Area"));
        await _lab.InvestigateAsync(id);
        Assert.That((await _lab.Rig.Store.GetInvestigationAsync(id))!.Status, Is.EqualTo("failed"));
        Assert.That((await _lab.ReportAsync(id))["model_verdict"], Is.Null);
    }

    // ---- the background host ----

    [Test, CancelAfter(30000)]
    public async Task Queued_investigations_run_in_the_background_oldest_first_one_at_a_time()
    {
        var first = await _lab.KeyedIncidentAsync();
        await _lab.Rig.FeedAsync(_lab.Rig.Keyed("task.list.unavailable", RecordLevel.Error, trace: new string('b', 32)));
        var second = (await _lab.Rig.AllIncidentsAsync()).Single(i => i.Id != first).Id;
        var gate = new TaskCompletionSource();
        var inFlight = 0;
        var maxInFlight = 0;
        _lab.Small.Before = async _ =>
        {
            maxInFlight = Math.Max(maxInFlight, Interlocked.Increment(ref inFlight));
            await gate.Task;
            Interlocked.Decrement(ref inFlight);
        };
        _lab.Small.Say(Report(evidence: ["E1"], cause: "First one.")).Say(Report(evidence: ["E1"], cause: "Second one."));

        await _lab.Host.StartAsync(CancellationToken.None);
        try
        {
            await RunHarness.WaitForAsync(() => Task.FromResult(_lab.Host.IsAvailable), "the host check");
            var queuing = Task.WhenAll(_lab.Host.QueueAsync(new InvestigationRequest(first, "t"), default), _lab.Host.QueueAsync(new InvestigationRequest(second, "t"), default));
            Assert.That(await Task.WhenAny(queuing, Task.Delay(2000)), Is.SameAs(queuing), "queueing returns at once while the host is busy");
            await RunHarness.WaitForAsync(() => Task.FromResult(_lab.Small.Requests.Count == 1), "the first generation");
            await RunHarness.WaitForAsync(async () => (await _lab.Rig.Store.GetInvestigationAsync(first))?.Status == "running", "running");
            Assert.That((await _lab.Rig.Store.GetInvestigationAsync(second))!.Status, Is.Not.EqualTo("running"), "the second waits its turn");

            // meanwhile the analyser is not held up: a new record becomes an incident while the model is thinking
            await _lab.Rig.FeedAsync(_lab.Rig.Keyed("org.view.unavailable", RecordLevel.Error, trace: new string('c', 32)));
            Assert.That((await _lab.Rig.AllIncidentsAsync()), Has.Count.EqualTo(3));
            gate.SetResult();
            await RunHarness.WaitForAsync(async () => (await _lab.Rig.Store.GetInvestigationAsync(second))?.Status == "done", "the second investigation");
        }
        finally { gate.TrySetResult(); await _lab.Host.StopAsync(CancellationToken.None); }

        Assert.That(maxInFlight, Is.EqualTo(1));
        Assert.That((await _lab.ReportAsync(first))["model_verdict"]!["cause"]!.GetValue<string>(), Is.EqualTo("First one."));
        Assert.That((await _lab.ReportAsync(second))["model_verdict"]!["cause"]!.GetValue<string>(), Is.EqualTo("Second one."));
    }

    [Test, CancelAfter(30000)]
    public async Task A_residual_incident_is_queued_by_the_processor_investigated_and_written_with_the_host_in_place_of_the_seam()
    {
        Lab? created = null;
        using var lab = created = new Lab(services: s => s.AddSingleton<IInvestigator>(sp => new InvestigatorHost(
            sp.GetRequiredService<IRunContextProvider>(), sp.GetRequiredService<IIncidentStore>(), created!.Options, created.Factory, Protocol.Load(),
            sp.GetRequiredService<TimeProvider>(), NullLogger<InvestigatorHost>.Instance, () => 64_000)));
        var wired = lab;
        var host = (InvestigatorHost)lab.Rig.Services.GetRequiredService<IInvestigator>();
        lab.Small.Say(Report(evidence: ["E1"], category: "unknown", cause: "A widget failed inside its engine."));
        await host.StartAsync(CancellationToken.None);
        try
        {
            await RunHarness.WaitForAsync(() => Task.FromResult(host.IsAvailable), "the host check");
            await wired.Rig.FeedAsync(Rig.Unknown("A widget exploded", RecordLevel.Error));
            var id = (await wired.Rig.AllIncidentsAsync()).Single().Id;
            await RunHarness.WaitForAsync(async () => (await wired.Rig.Store.GetInvestigationAsync(id))?.Status == "done", "the investigation");
            var report = await wired.ReportAsync(id);
            Assert.That(report["trigger"]!.GetValue<string>(), Is.EqualTo("unknown"));
            Assert.That(report["rule_verdict"], Is.Null, "no signature, no rule verdict");
            Assert.That(report["model_verdict"]!["cause"]!.GetValue<string>(), Does.Contain("widget"));
            Assert.That(report["actions_taken"]!.AsArray().Select(a => a!["action"]!.GetValue<string>()), Is.SupersetOf(new[] { "investigation_queued", "investigation_done" }));
            var user = lab.Small.Requests.Single().Messages[1].Content;
            Assert.That(user, Does.Contain("\"trigger\":\"unknown\"").And.Contain("\"signature\":{\"kind\":\"none\"").And.Contain("<pack>null</pack>"));
            Assert.That(user, Does.Contain("A widget exploded"), "the masked triggering record is E1");
        }
        finally { await host.StopAsync(CancellationToken.None); }
    }

    [Test]
    public async Task Without_a_host_a_residual_incident_is_skipped_and_the_pipeline_goes_on()
    {
        using var lab = new Lab(writeLock: false);
        lab.WriteLock("{nope");
        await lab.Host.RefreshAsync(CancellationToken.None);
        Assert.That(lab.Host.IsAvailable, Is.False);
        Assert.That(lab.Host.Status.Reason, Does.Contain("cannot be read").And.Contain("rules only"));
        await lab.Rig.FeedAsync(Rig.Unknown());
        var id = (await lab.Rig.AllIncidentsAsync()).Single().Id;
        Assert.That((await lab.Rig.Store.GetInvestigationAsync(id))!.Status, Is.EqualTo("skipped"), "the recording investigator was not available: skipped, the incident is written");
    }
}
