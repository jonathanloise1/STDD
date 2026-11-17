using System.Text.Json.Nodes;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Rules;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Tools;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

public class RuleEngineTests
{
    private const string OrgId = "0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91";
    private const string Trace = "4bf92f3577b34da6a3ce929d0e0e4736";

    private FixtureRepo _repo = null!;
    private OperationsPack _pack = null!;
    private RecordingAuditSink _audit = null!;
    private TempDir _world = null!;

    [OneTimeSetUp]
    public void Build()
    {
        _repo = new FixtureRepo();
        _pack = _repo.BuildAndLoad();
    }

    [OneTimeTearDown]
    public void Cleanup() => _repo.Dispose();

    [SetUp]
    public void SetUp()
    {
        _audit = new RecordingAuditSink();
        _world = new TempDir();
        CopyWorld(FixtureRepo.WorldRoot, _world.Path);
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    private static void CopyWorld(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from)) CopyWorld(d, Path.Combine(to, Path.GetFileName(d)));
    }

    private (RuleEngine Engine, ToolExecutor Executor) Engine(ToolBindings? bindings = null)
    {
        var time = FakeTime.Default();
        var services = new ToolServices(_pack.Entities, _pack.Masking, new StaticPackProvider(_pack), null, null, null, null, _world.Path, time);
        var executor = new ToolExecutor(_pack.Tools, _pack.Entities, _pack.Constraints, Fx.Masker(), bindings ?? ToolBindings.ForWorld(_pack.Tools, _world.Path),
            BuiltInTools.Create(services), [_audit], time);
        return (new RuleEngine(executor, _pack.Entities, time), executor);
    }

    private static LogRecord Record(string key = "task.create.access_lost", string? trace = Trace, string? org = OrgId) => Fx.Record(
        message: "Task not created", level: RecordLevel.Warning, eventName: key, traceId: trace,
        props: org is null ? new() : new() { ["OrganizationId"] = org, ["UserId"] = "6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72" });

    private Escalation Escalate(LogRecord record, string trigger = Triggers.Severity, bool residual = false)
    {
        var sig = record.LogKey is null ? null : _pack.FindByLogKey(record.LogKey);
        var match = sig is null ? new MatchResult(MatchKind.UnknownKey, null, record.LogKey ?? "?", null) : new MatchResult(MatchKind.Matched, sig, sig.Id, null);
        return new Escalation(record, match, trigger, sig?.Severity ?? "Error", residual, "fp", match.Id, 1, 1, 1, 1, EscalationAction.OpenNew, null, null);
    }

    [Test]
    public async Task Correlation_reads_the_trace_and_the_entities_named_by_fingerprint_by_and_records_evidence()
    {
        var (engine, _) = Engine();
        var result = await engine.RunAsync(Escalate(Record()), new IncidentToolSession("INC-1"));

        Assert.That(result.TraceIds, Is.EqualTo(new[] { Trace }));
        Assert.That(result.Entities, Is.EqualTo(new[] { new EntityRef("organization", OrgId) }));
        Assert.That(result.Evidence.Select(e => e.Id), Is.EqualTo(new[] { "E1", "E2", "E3", "E4", "E5" }));
        Assert.That(result.Evidence.Select(e => e.Source), Is.EqualTo(new[] { "signal", "logs_by_correlation", "sql_entity_read", "audit_trail_by_entity", "health_status" }));
        Assert.That(result.Evidence[0].Reference, Does.Contain("task.create.access_lost"));
        Assert.That(result.Evidence[1].Reference, Is.EqualTo($"logs_by_correlation trace_id={Trace}"));
        Assert.That(result.Evidence[3].Reference, Is.EqualTo($"audit_trail_by_entity entity=organization id={OrgId}"));
        Assert.That(result.Evidence.All(e => e.Excerpt.Length <= 1000), Is.True);
        Assert.That(result.Evidence[2].Excerpt, Does.Contain("IsDeleted"));
        Assert.That(result.Evidence[2].Excerpt, Does.Not.Contain("must not leave"), "masked at the tool");
        Assert.That(_audit.Rows.Select(r => r.Caller).Distinct(), Is.EqualTo(new[] { "rules" }));
        Assert.That(_audit.Rows.Select(r => r.Tool), Is.EqualTo(new[] { "logs_by_correlation", "sql_entity_read", "audit_trail_by_entity", "health_status" }));
        Assert.That(result.RunbookAnchor, Is.EqualTo("task-create-access-lost"));
        Assert.That(result.RunbookText, Does.StartWith("### Meaning"));
    }

    [Test]
    public async Task A_failed_check_makes_the_incident_residual_whatever_its_trigger_and_halves_the_confidence()
    {
        var (engine, _) = Engine();   // the world's health is Degraded: 'status == "Healthy"' fails
        var result = await engine.RunAsync(Escalate(Record(), Triggers.Severity, residual: false), new IncidentToolSession("INC-1"));
        var check = result.Checks.Single();
        Assert.That(check.Tool, Is.EqualTo("health_status"));
        Assert.That(check.Expect, Is.EqualTo("status == \"Healthy\""));
        Assert.That(check.Passed, Is.False);
        Assert.That(check.EvidenceId, Is.EqualTo("E5"));
        Assert.That(result.ChecksFailed, Is.True);
        Assert.That(result.Residual, Is.True);
        Assert.That(result.Verdict!.Confidence, Is.EqualTo(0.5));
    }

    [Test]
    public async Task When_every_check_passes_the_verdict_has_confidence_1_and_a_rule_trigger_stays_a_rule_case()
    {
        File.WriteAllText(Path.Combine(_world.Path, "health.json"), """{"status":"Healthy","checks":[]}""");
        var (engine, _) = Engine();
        var result = await engine.RunAsync(Escalate(Record()), new IncidentToolSession("INC-1"));
        Assert.That(result.Checks.Single().Passed, Is.True);
        Assert.That(result.Residual, Is.False);
        var verdict = result.Verdict!;
        Assert.That(verdict.Confidence, Is.EqualTo(1.0));
        Assert.That(verdict.CauseCategory, Is.EqualTo("access"));
        Assert.That(verdict.Cause, Is.EqualTo("A member tried to create a task in an organization they had just been removed from. Expected after team changes."));
        Assert.That(verdict.EvidenceRefs, Is.EqualTo(new[] { "E1", "E5" }), "E1 and the check results");
    }

    [Test]
    public async Task A_residual_trigger_stays_residual_even_when_every_check_passes()
    {
        File.WriteAllText(Path.Combine(_world.Path, "health.json"), """{"status":"Healthy","checks":[]}""");
        var (engine, _) = Engine();
        var result = await engine.RunAsync(Escalate(Record(), Triggers.Rate, residual: true), new IncidentToolSession("INC-1"));
        Assert.That(result.Residual, Is.True);
        Assert.That(result.Verdict!.Confidence, Is.EqualTo(1.0));
    }

    [Test]
    public async Task An_unknown_signal_gets_correlation_but_no_verdict_and_no_checks()
    {
        var (engine, _) = Engine();
        var record = Fx.Record(message: "boom", level: RecordLevel.Error, traceId: Trace);
        var escalation = new Escalation(record, new MatchResult(MatchKind.UnknownTemplate, null, "t-abc", null), Triggers.Unknown, "Error", true, "fp", "t-abc", 1, 1, 1, 1, EscalationAction.OpenNew, null, null);
        var result = await engine.RunAsync(escalation, new IncidentToolSession());
        Assert.That(result.Verdict, Is.Null);
        Assert.That(result.Checks, Is.Empty);
        Assert.That(result.Signature, Is.Null);
        Assert.That(result.Evidence.Select(e => e.Id), Is.EqualTo(new[] { "E1", "E2" }));
        Assert.That(result.Residual, Is.True);
    }

    [Test]
    public async Task A_record_without_a_trace_id_skips_the_log_correlation()
    {
        var (engine, _) = Engine();
        var result = await engine.RunAsync(Escalate(Record(trace: null, org: null)), new IncidentToolSession("INC-1"));
        Assert.That(result.TraceIds, Is.Empty);
        Assert.That(result.Evidence.Select(e => e.Source), Is.EqualTo(new[] { "signal", "health_status" }));
    }

    [Test]
    public async Task Properties_that_do_not_name_an_entity_key_are_ignored()
    {
        var sig = _pack.FindByLogKey("task.create.access_lost")!;
        var original = sig.FingerprintBy.ToList();
        sig.FingerprintBy.Clear();
        sig.FingerprintBy.Add("UserId");   // no entity named 'user'? it is: UserId -> user
        try
        {
            var (engine, _) = Engine();
            var r = await engine.RunAsync(Escalate(Record()), new IncidentToolSession("INC-1"));
            Assert.That(r.Entities.Single().Entity, Is.EqualTo("user"), "{Entity}Id names the entity, case-insensitively");
            sig.FingerprintBy.Clear();
            sig.FingerprintBy.Add("Route");
            var r2 = await engine.RunAsync(Escalate(Record()), new IncidentToolSession("INC-2"));
            Assert.That(r2.Entities, Is.Empty);
        }
        finally
        {
            sig.FingerprintBy.Clear();
            sig.FingerprintBy.AddRange(original);
        }
    }

    [Test]
    public async Task A_check_that_cannot_run_fails_without_stopping_the_run()
    {
        // a profile that does not offer health_status: the check is denied by the executor
        var bindings = new ToolBindings(ToolBindings.ForWorld(_pack.Tools, _world.Path).ToolNames.Where(n => n != "health_status")
            .ToDictionary(n => n, n => ToolBindings.ForWorld(_pack.Tools, _world.Path).For(n)!));
        var (engine, _) = Engine(bindings);
        var result = await engine.RunAsync(Escalate(Record()), new IncidentToolSession("INC-1"));
        Assert.That(result.Checks.Single().Passed, Is.False);
        Assert.That(result.Checks.Single().Error, Does.Contain("not offered"));
        Assert.That(result.Residual, Is.True);
        Assert.That(result.Evidence.Select(e => e.Source), Does.Contain("logs_by_correlation"), "the rest of the run still happened");
    }

    [Test]
    public async Task A_check_that_repeats_a_correlation_call_reuses_its_data_instead_of_a_duplicate()
    {
        var sig = _pack.FindByLogKey("task.create.access_lost")!;
        var original = sig.RuleChecks.ToList();
        sig.RuleChecks.Clear();
        sig.RuleChecks.Add(new RuleCheck { Tool = "logs_by_correlation", Arguments = Fx.Obj("""{"trace_id":"{signal.TraceId}"}"""), Expect = "records exists" });
        try
        {
            var (engine, _) = Engine();
            var result = await engine.RunAsync(Escalate(Record()), new IncidentToolSession("INC-1"));
            Assert.That(result.Checks.Single().Passed, Is.True);
            Assert.That(result.Checks.Single().EvidenceId, Is.EqualTo("E2"), "the earlier call's evidence");
            Assert.That(_audit.Rows.Count(r => r.Tool == "logs_by_correlation"), Is.EqualTo(1));
        }
        finally
        {
            sig.RuleChecks.Clear();
            sig.RuleChecks.AddRange(original);
        }
    }

    [Test]
    public async Task Evidence_and_verdict_fit_the_report_limits()
    {
        var (engine, _) = Engine();
        var result = await engine.RunAsync(Escalate(Record()), new IncidentToolSession("INC-1"));
        Assert.That(result.Evidence.Count, Is.LessThanOrEqualTo(40));
        Assert.That(result.Verdict!.EvidenceRefs.Count, Is.LessThanOrEqualTo(10));
        Assert.That(result.Verdict.Cause.Length, Is.LessThanOrEqualTo(300));
    }

    // ---- arguments ----

    [Test]
    public void Placeholders_are_resolved_from_the_triggering_record()
    {
        var record = Record();
        Assert.That(RuleEngine.TryResolveArguments(Fx.Obj("""{"trace_id":"{signal.TraceId}","id":"{signal.Properties.OrganizationId}","entity":"organization","limit":5,"mixed":"t-{signal.TraceId}-x"}"""),
            record, out var args, out var missing), Is.True, missing);
        Assert.That(args["trace_id"]!.GetValue<string>(), Is.EqualTo(Trace));
        Assert.That(args["id"]!.GetValue<string>(), Is.EqualTo(OrgId));
        Assert.That(args["entity"]!.GetValue<string>(), Is.EqualTo("organization"));
        Assert.That(args["limit"]!.GetValue<int>(), Is.EqualTo(5));
        Assert.That(args["mixed"]!.GetValue<string>(), Is.EqualTo($"t-{Trace}-x"));
    }

    [TestCase("""{"id":"{signal.Properties.Nothing}"}""", "{signal.Properties.Nothing}")]
    [TestCase("""{"id":"{signal.Unknown}"}""", "{signal.Unknown}")]
    public void An_unresolvable_placeholder_is_reported(string template, string expectedMissing)
    {
        Assert.That(RuleEngine.TryResolveArguments(Fx.Obj(template), Record(), out _, out var missing), Is.False);
        Assert.That(missing, Is.EqualTo(expectedMissing));
    }

    [Test]
    public void Meaning_is_the_text_of_the_meaning_section()
    {
        Assert.That(RuleEngine.Meaning("### Meaning\n\nOne.\nTwo.\n\n### Check\n\n1. x"), Is.EqualTo("One. Two."));
        Assert.That(RuleEngine.Meaning("### Check\n\n1. x"), Is.Empty);
    }
}
