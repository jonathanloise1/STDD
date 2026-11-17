using System.Text.Json.Nodes;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Store;
using MyApp.Monitor.Tools;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

/// <summary>Step 6 of the incident loop: an escalation becomes one incident report that validates against the schema, or folds into the open one.</summary>
public class IncidentProcessorTests
{
    private Rig _rig = null!;

    [SetUp]
    public void SetUp() => _rig = new Rig();

    [TearDown]
    public void TearDown() => _rig.Dispose();

    private static SchemaSubsetValidator ReportSchema() => new(JsonNode.Parse(Fx.Support("incident-report.schema.json"))!);

    private static void AssertValid(IncidentRow row)
    {
        Assert.That(ReportSchema().IsValid(row.Report, out var errors), Is.True, string.Join("; ", errors));
    }

    private static OperationsPack Variant(Action<FixtureRepo> change)
    {
        using var repo = new FixtureRepo();
        change(repo);
        return repo.BuildAndLoad();
    }

    private static IEnumerable<LogRecord> Burst(Rig rig, int count, string key = "task.create.access_lost") =>
        Enumerable.Range(0, count).Select(i => rig.Keyed(key, at: rig.Time.GetUtcNow().AddSeconds(i), trace: $"{i + 1:x32}"));

    // ---- the report ----

    [Test]
    public async Task An_error_escalation_becomes_an_incident_whose_report_validates_against_the_schema()
    {
        var results = await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error));

        Assert.That(results.Single().Escalation!.Trigger, Is.EqualTo("severity"));
        var row = (await _rig.AllIncidentsAsync()).Single();
        AssertValid(row);
        var report = row.Report;
        Assert.That(row.Id, Is.EqualTo("INC-20261106-0001"));
        Assert.That((row.Status, row.Severity, row.Trigger, row.Environment, row.VerdictSource), Is.EqualTo(("open", "Error", "severity", "prod", "rule")));
        Assert.That(report["signature"]!["kind"]!.GetValue<string>(), Is.EqualTo("keyed"));
        Assert.That(report["signature"]!["log_key"]!.GetValue<string>(), Is.EqualTo("task.create.unavailable"));
        Assert.That(report["area"]!.GetValue<string>(), Is.EqualTo("tasks"));
        Assert.That(report["test_case"]!.GetValue<string>(), Is.EqualTo("TASK-CREATE-008"));
        Assert.That(report["rule_verdict"]!["cause_category"]!.GetValue<string>(), Is.EqualTo("dependency_unavailable"));
        Assert.That(report["rule_verdict"]!["confidence"]!.GetValue<double>(), Is.EqualTo(1.0));
        Assert.That(report["model_verdict"], Is.Null);
        Assert.That(report["investigation"], Is.Null);
        Assert.That(report["model"], Is.Null);
        Assert.That(report["runbook"]!["anchor"]!.GetValue<string>(), Is.EqualTo("task-create-unavailable"));
        Assert.That(report["previous_incident_id"], Is.Null);
        Assert.That(report["outcome"], Is.Null);
        Assert.That(report["pack_version"]!.GetValue<string>(), Is.EqualTo("1.4.0+abc1234"));
        Assert.That(report["actions_taken"]![0]!["action"]!.GetValue<string>(), Is.EqualTo("created"));
    }

    [Test]
    public async Task The_report_validates_against_the_schema_file_of_the_repository_not_only_the_one_in_the_pack()
    {
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error));
        var schemaFile = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "docs", "support", "incident-report.schema.json"));
        if (!File.Exists(schemaFile)) Assert.Ignore("the repository schema is not next to the build output");
        var validator = new SchemaSubsetValidator(JsonNode.Parse(File.ReadAllText(schemaFile))!);
        Assert.That(validator.IsValid((await _rig.AllIncidentsAsync()).Single().Report, out var errors), Is.True, string.Join("; ", errors));
    }

    [Test]
    public async Task Every_evidence_item_has_a_reference_a_person_can_open_and_a_masked_excerpt()
    {
        var record = _rig.Keyed("task.create.unavailable", RecordLevel.Error, message: "Task not created: jane.doe@example.com could not save") with { Origin = "/var/log/myapp/myapp-api-20261106.jsonl" };
        await _rig.FeedAsync(record);

        var evidence = (await _rig.AllIncidentsAsync()).Single().Report["evidence"]!.AsArray();
        Assert.That(evidence.Select(e => e!["id"]!.GetValue<string>()), Is.EqualTo(new[] { "E1", "E2", "E3", "E4", "E5" }.Take(evidence.Count)));
        foreach (var e in evidence)
        {
            Assert.That(e!["reference"]!.GetValue<string>(), Is.Not.Empty);
            Assert.That(e["excerpt"]!.GetValue<string>().Length, Is.LessThanOrEqualTo(1000));
            Assert.That(e["observed_at"]!.GetValue<string>(), Does.EndWith("Z"));
        }
        var first = evidence[0]!;
        Assert.That(first["source"]!.GetValue<string>(), Is.EqualTo("signal"));
        Assert.That(first["reference"]!.GetValue<string>(), Does.Contain("/var/log/myapp/myapp-api-20261106.jsonl").And.Contain("log key task.create.unavailable").And.Contain(Rig.Trace));
        Assert.That(first["excerpt"]!.GetValue<string>(), Is.EqualTo("Task not created: [email] could not save"));
        Assert.That(evidence.Skip(1).Select(e => e!["reference"]!.GetValue<string>()), Has.Some.EqualTo($"logs_by_correlation trace_id={Rig.Trace}"));
    }

    [Test]
    public async Task A_planted_e_mail_address_never_reaches_the_store()
    {
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error, message: "Task not created: jane.doe@example.com could not save"));
        await _rig.Store.WriteAsync(new ToolAuditRow(DateTimeOffset.UtcNow, null, "x", "t", "{}", null, "ok", 0, 0, 0, null), default);
        _rig.Store.Database.Close();   // flush the WAL into the main file as well

        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(_rig.DbPath)!))
            Assert.That(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(file)), Does.Not.Contain("jane.doe@example.com"), file);
        Assert.That((await _rig.AllIncidentsAsync()).Single().ReportJson, Does.Contain("[email]"));
    }

    [Test]
    public async Task The_rule_engine_runs_for_a_new_incident_with_the_incident_id_in_the_tool_audit()
    {
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error));
        var audit = await _rig.Store.ToolAuditAsync("INC-20261106-0001");
        Assert.That(audit.Select(a => a.Tool), Does.Contain("logs_by_correlation"));
        Assert.That(audit.Select(a => a.Caller).Distinct(), Is.EqualTo(new[] { "rules" }));
    }

    // ---- dedup ----

    [Test]
    public async Task A_second_record_of_an_open_fingerprint_folds_into_its_incident_and_sends_nothing_new()
    {
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error, trace: Rig.Trace));
        var sentBefore = (_rig.Email.Sent.Count, _rig.Webhook.Requests.Count);
        _rig.Time.Advance(TimeSpan.FromMinutes(1));
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error, trace: "7d2e0c4f9a1b48e3b5c6d7e8f9a0b1c2", message: "second"));

        var row = (await _rig.AllIncidentsAsync()).Single();
        AssertValid(row);
        var report = row.Report;
        Assert.That(row.Count, Is.EqualTo(2));
        Assert.That(report["last_seen"]!.GetValue<string>(), Does.Contain("12:01"));
        Assert.That(report["correlation"]!["trace_ids"]!.AsArray().Select(t => t!.GetValue<string>()), Is.EqualTo(new[] { Rig.Trace, "7d2e0c4f9a1b48e3b5c6d7e8f9a0b1c2" }));
        Assert.That(report["evidence"]!.AsArray().Last()!["excerpt"]!.GetValue<string>(), Is.EqualTo("second"));
        Assert.That(report["actions_taken"]!.AsArray().Select(a => a!["action"]!.GetValue<string>()), Does.Contain("dedup_hit"));
        Assert.That((_rig.Email.Sent.Count, _rig.Webhook.Requests.Count), Is.EqualTo(sentBefore), "no new notification except on a severity raise");
        Assert.That((await _rig.Store.EventsAsync(row.Id)).Select(e => e.Kind), Does.Contain("record_appended"));
    }

    [Test]
    public async Task Appended_trace_ids_stop_at_50_and_appended_evidence_at_20_items()
    {
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error));
        for (var i = 0; i < 70; i++)
            await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error, trace: $"{i + 100:x32}", message: $"again {i}", at: _rig.Time.GetUtcNow().AddSeconds(i + 1)));

        var row = (await _rig.AllIncidentsAsync()).Single();
        AssertValid(row);
        Assert.That(row.Count, Is.EqualTo(71));
        Assert.That(row.Report["correlation"]!["trace_ids"]!.AsArray().Count, Is.EqualTo(50));
        var signalEvidence = row.Report["evidence"]!.AsArray().Where(e => e!["source"]!.GetValue<string>() == "signal" && e["id"]!.GetValue<string>() != "E1");
        Assert.That(signalEvidence.Count(), Is.EqualTo(20));
        Assert.That(row.Report["evidence"]!.AsArray().Count, Is.LessThanOrEqualTo(40));
        Assert.That(row.Report["actions_taken"]!.AsArray().Count(a => a!["action"]!.GetValue<string>() == "dedup_hit"), Is.EqualTo(1), "one dedup entry per hour");
    }

    [Test]
    public async Task A_severity_raise_updates_the_incident_and_sends_one_more_e_mail()
    {
        // an unknown template at Warning escalates at the third record; the same template at Error raises it
        for (var i = 0; i < 3; i++) await _rig.FeedAsync(Rig.Unknown("A widget slipped", RecordLevel.Warning, _rig.Time.GetUtcNow().AddSeconds(i)));
        Assert.That(_rig.Email.Sent, Is.Empty, "Warning is below the e-mail gate");
        await _rig.FeedAsync(Rig.Unknown("A widget slipped", RecordLevel.Error, _rig.Time.GetUtcNow().AddSeconds(5)));

        var row = (await _rig.AllIncidentsAsync()).Single();
        AssertValid(row);
        Assert.That(row.Severity, Is.EqualTo("Error"));
        Assert.That(row.Report["actions_taken"]!.AsArray().Select(a => a!["action"]!.GetValue<string>()), Does.Contain("severity_raised").And.Contain("email_sent"));
        Assert.That(_rig.Email.Sent.Count, Is.EqualTo(1));
        Assert.That((await _rig.Store.FindNotificationAsync(row.Id, "email", "severity_raised"))!.Kind, Is.EqualTo("sent"));
    }

    // ---- residual cases and the investigator seam ----

    [Test]
    public async Task A_rate_escalation_is_residual_written_with_its_rule_verdict_and_an_empty_model_with_the_investigation_skipped()
    {
        await _rig.FeedAsync(Burst(_rig, 16).ToArray());

        var row = (await _rig.AllIncidentsAsync()).Single();
        AssertValid(row);
        Assert.That((row.Trigger, row.Severity, row.VerdictSource), Is.EqualTo(("rate", "Warning", "rule")));
        Assert.That(row.Report["model"], Is.Null);
        Assert.That(row.Report["model_verdict"], Is.Null);
        Assert.That(row.Report["rule_verdict"], Is.Not.Null);
        var investigation = (await _rig.Store.GetInvestigationAsync(row.Id))!;
        Assert.That(investigation.Status, Is.EqualTo("skipped"));
        Assert.That((await _rig.Store.EventsAsync(row.Id)).Select(e => e.Kind), Does.Contain("investigation"));
    }

    [Test]
    public async Task With_an_investigator_available_a_residual_incident_is_queued_after_it_was_written()
    {
        _rig.Dispose();
        _rig = new Rig(investigatorAvailable: true);
        await _rig.FeedAsync(Burst(_rig, 16).ToArray());

        var row = (await _rig.AllIncidentsAsync()).Single();
        Assert.That(_rig.Investigator.Queued.Single(), Is.EqualTo(new MyApp.Monitor.Investigator.InvestigationRequest(row.Id, "rate")));
        Assert.That((await _rig.Store.GetInvestigationAsync(row.Id))!.Status, Is.EqualTo("queued"));
        Assert.That(row.Report["actions_taken"]!.AsArray().Select(a => a!["action"]!.GetValue<string>()), Does.Contain("investigation_queued"));
    }

    [Test]
    public async Task A_rule_case_is_never_queued_for_investigation()
    {
        _rig.Dispose();
        _rig = new Rig(investigatorAvailable: true);
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error));
        Assert.That(_rig.Investigator.Queued, Is.Empty);
        Assert.That(await _rig.Store.GetInvestigationAsync("INC-20261106-0001"), Is.Null);
    }

    [Test]
    public async Task A_failed_rule_check_makes_a_severity_case_residual_and_halves_the_confidence()
    {
        var pack = Variant(r => r.Replace(FixtureRepo.Ops,
            "The task could not be saved because a dependency failed. The user was asked to retry.\n\n### Check\n\n1. Read the health of the application with `health_status`.\n\n### Rule checks\n\nNone.",
            "The task could not be saved because a dependency failed. The user was asked to retry.\n\n### Check\n\n1. Read the health of the application with `health_status`.\n\n### Rule checks\n\n| Tool | Arguments | Expect |\n|------|-----------|--------|\n| health_status | - | status == \"Healthy\" |"));
        _rig.Dispose();
        _rig = new Rig(pack, health: """{"status":"Degraded","checks":[{"name":"sqlserver","status":"Degraded","durationMs":900}]}""");

        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error));
        var row = (await _rig.AllIncidentsAsync()).Single();
        AssertValid(row);
        Assert.That(row.Report["rule_verdict"]!["confidence"]!.GetValue<double>(), Is.EqualTo(0.5));
        Assert.That(row.Report["runbook"]!["checks"]![0]!["passed"]!.GetValue<bool>(), Is.False);
        Assert.That((await _rig.Store.GetInvestigationAsync(row.Id))!.Status, Is.EqualTo("skipped"), "residual because the evidence contradicts the runbook");
    }

    [Test]
    public async Task An_unknown_error_is_residual_with_no_rule_verdict_and_signature_kind_none()
    {
        await _rig.FeedAsync(Rig.Unknown("A widget exploded"));

        var row = (await _rig.AllIncidentsAsync()).Single();
        AssertValid(row);
        Assert.That((row.Trigger, row.VerdictSource), Is.EqualTo(("unknown", "none")));
        Assert.That(row.Report["rule_verdict"], Is.Null);
        Assert.That(row.Report["runbook"], Is.Null);
        Assert.That(row.Report["signature"]!["kind"]!.GetValue<string>(), Is.EqualTo("none"));
        Assert.That(row.Report["signature"]!["template_id"]!.GetValue<string>(), Does.StartWith("t-"));
        Assert.That(row.Report["signature"]!["template"]!.GetValue<string>(), Does.Contain("widget"));
        Assert.That(row.Report["area"], Is.Null);
        Assert.That((await _rig.Store.GetInvestigationAsync(row.Id))!.Status, Is.EqualTo("skipped"));
        Assert.That(((IUnknownTemplateSink)_rig.Store).All().Single().Count, Is.EqualTo(1));
    }

    [Test]
    public async Task An_unknown_log_key_is_an_incident_with_the_key_as_signature_id()
    {
        await _rig.FeedAsync(_rig.Keyed("task.update.brand_new", RecordLevel.Error));
        var row = (await _rig.AllIncidentsAsync()).Single();
        AssertValid(row);
        Assert.That(row.Report["signature"]!["kind"]!.GetValue<string>(), Is.EqualTo("none"));
        Assert.That(row.Report["signature"]!["log_key"]!.GetValue<string>(), Is.EqualTo("task.update.brand_new"));
    }

    [Test]
    public async Task A_health_event_matches_the_template_runbook_and_is_a_rule_case()
    {
        await _rig.FeedAsync(Rig.Health());
        var row = (await _rig.AllIncidentsAsync()).Single();
        AssertValid(row);
        Assert.That((row.Trigger, row.VerdictSource, row.Severity), Is.EqualTo(("health", "rule", "Error")));
        Assert.That(row.Report["signature"]!["kind"]!.GetValue<string>(), Is.EqualTo("template"));
        Assert.That(row.Report["signature"]!["id"]!.GetValue<string>(), Is.EqualTo("t-2bb5dcdc1949"));
    }

    // ---- noise, regression, closed incidents ----

    [Test]
    public async Task Accepted_noise_produces_counters_and_not_incidents_or_notifications()
    {
        var pack = Variant(r => r.Replace(FixtureRepo.Ops,
            "| Expected rate | 5 per hour |\n| Fingerprint by | OrganizationId |\n| Cause category | access |\n| Status | Active |\n| Fixed in | None |\n| Noise rationale | None |\n| Noise expires | None |",
            "| Expected rate | 5 per hour |\n| Fingerprint by | OrganizationId |\n| Cause category | access |\n| Status | Noise |\n| Fixed in | None |\n| Noise rationale | Accepted after team changes |\n| Noise expires | 2027-01-01 |"));
        _rig.Dispose();
        _rig = new Rig(pack);

        var results = await _rig.FeedAsync(Burst(_rig, 12).ToArray());

        Assert.That(results.All(r => r.Escalation is null), Is.True);
        Assert.That(await _rig.AllIncidentsAsync(), Is.Empty);
        Assert.That(_rig.Email.Sent, Is.Empty);
        Assert.That(_rig.Webhook.Requests, Is.Empty);
        Assert.That(((ICounterStore)_rig.Store).Snapshot().Sum(c => c.Count), Is.EqualTo(12), "counted, never silently dropped");
        Assert.That(results[0].CountedOnlyReason, Does.Contain("accepted noise"));
    }

    [Test]
    public async Task Noise_above_its_context_escalates_as_a_residual_case()
    {
        var pack = Variant(r => r.Replace(FixtureRepo.Ops,
            "| Status | Active |\n| Fixed in | None |\n| Noise rationale | None |\n| Noise expires | None |\n\n### Meaning\n\nA member tried",
            "| Status | Noise |\n| Fixed in | None |\n| Noise rationale | Accepted |\n| Noise expires | 2027-01-01 |\n\n### Meaning\n\nA member tried"));
        _rig.Dispose();
        _rig = new Rig(pack);
        await _rig.FeedAsync(Burst(_rig, 16).ToArray());
        var row = (await _rig.AllIncidentsAsync()).Single();
        Assert.That(row.Trigger, Is.EqualTo("noise_out_of_context"));
        Assert.That((await _rig.Store.GetInvestigationAsync(row.Id))!.Status, Is.EqualTo("skipped"));
    }

    [Test]
    public async Task A_fixed_signature_that_fires_again_opens_a_regression_with_the_severity_raised_one_step_and_an_automatic_ticket()
    {
        var pack = Variant(r => r.Replace(FixtureRepo.Ops,
            "| Status | Active |\n| Fixed in | None |\n| Noise rationale | None |\n| Noise expires | None |\n\n### Meaning\n\nA member tried",
            "| Status | Fixed |\n| Fixed in | 1.4.0 |\n| Noise rationale | None |\n| Noise expires | None |\n\n### Meaning\n\nA member tried"));
        _rig.Dispose();
        _rig = new Rig(pack);

        await _rig.FeedAsync(_rig.Keyed(version: "1.3.9"));
        Assert.That(await _rig.AllIncidentsAsync(), Is.Empty, "an older version is not a regression");
        var again = await _rig.FeedAsync(_rig.Keyed(version: "1.4.1", at: _rig.Time.GetUtcNow().AddSeconds(1)));
        Assert.That(again.Single().Escalation, Is.Not.Null, again.Single().CountedOnlyReason);

        var row = (await _rig.AllIncidentsAsync()).Single();
        AssertValid(row);
        Assert.That((row.Trigger, row.Severity), Is.EqualTo(("regression", "Error")));
        Assert.That(_rig.Webhook.Requests.Count, Is.EqualTo(1), "regression with a rule verdict creates the ticket");
        Assert.That(_rig.Email.Sent.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task After_the_incident_is_closed_a_new_record_opens_a_new_incident_that_links_the_previous_one()
    {
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error));
        Assert.That((await _rig.Actions.CloseAsync("INC-20261106-0001", new Dashboard.CloseRequest("fixed", null, null, null, null), "ann")).Ok, Is.True);
        _rig.Time.Advance(TimeSpan.FromMinutes(2));
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error, message: "again after the fix"));

        var rows = (await _rig.AllIncidentsAsync()).OrderBy(r => r.Id).ToList();
        Assert.That(rows.Select(r => (r.Id, r.Status)), Is.EqualTo(new[] { ("INC-20261106-0001", "closed"), ("INC-20261106-0002", "open") }));
        Assert.That(rows[1].PreviousIncidentId, Is.EqualTo("INC-20261106-0001"));
        AssertValid(rows[1]);
    }

    [TestCase("noise")]
    [TestCase("duplicate")]
    public async Task A_record_after_a_noise_or_duplicate_outcome_does_not_link_the_previous_incident(string outcome)
    {
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error));
        await _rig.FeedAsync(Rig.Unknown("A widget exploded"));   // another incident, to name as the original
        var original = (await _rig.AllIncidentsAsync()).OrderBy(r => r.Id).Last().Id;
        var result = await _rig.Actions.CloseAsync("INC-20261106-0001", new Dashboard.CloseRequest(outcome, null, null, null, outcome == "duplicate" ? original : null), "ann");
        Assert.That(result.Ok, Is.True, result.Message);
        _rig.Time.Advance(TimeSpan.FromMinutes(2));
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error, message: "again"));
        var newest = (await _rig.AllIncidentsAsync()).Single(r => r.Status == "open" && r.Fingerprint == (_rig.Store.Snapshot().First(c => c.SignatureOrTemplate == "task.create.unavailable").Fingerprint));
        Assert.That(newest.PreviousIncidentId, Is.Null);
    }

    // ---- suppression (10.4) ----

    [Test]
    public async Task Inside_a_suppression_window_matching_records_update_counters_only_and_the_summary_is_written_when_it_ends()
    {
        var starts = _rig.Time.GetUtcNow().AddMinutes(-5);
        Assert.That((await _rig.Actions.AddWindowAsync("deploy", starts, starts.AddMinutes(60), "task.create.*", "planned deploy", "ann")).Ok, Is.True);

        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error), _rig.Keyed("task.create.unavailable", RecordLevel.Error, message: "again"));

        Assert.That(await _rig.AllIncidentsAsync(), Is.Empty);
        Assert.That(_rig.Email.Sent, Is.Empty);
        Assert.That(((ICounterStore)_rig.Store).Snapshot().Sum(c => c.Count), Is.EqualTo(2), "counters are updated");

        _rig.Time.Advance(TimeSpan.FromMinutes(61));
        await _rig.Suppression.FlushEndedAsync(_rig.Time.GetUtcNow());
        var summary = (await _rig.Store.EventsAsync("window:deploy")).Single();
        Assert.That(summary.Kind, Is.EqualTo("suppression_summary"));
        var data = JsonNode.Parse(summary.DataJson)!;
        Assert.That(data["total"]!.GetValue<int>(), Is.EqualTo(2));
        Assert.That(data["counts"]!["task.create.unavailable"]!.GetValue<int>(), Is.EqualTo(2));

        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error, message: "after the window"));
        Assert.That((await _rig.AllIncidentsAsync()).Count, Is.EqualTo(1), "the window is over");
    }

    [Test]
    public async Task A_window_that_does_not_match_the_signature_suppresses_nothing()
    {
        var starts = _rig.Time.GetUtcNow().AddMinutes(-5);
        await _rig.Actions.AddWindowAsync("other", starts, starts.AddHours(1), "billing.*", null, "ann");
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error));
        Assert.That((await _rig.AllIncidentsAsync()).Count, Is.EqualTo(1));
    }

    // ---- manual incidents ----

    [Test]
    public async Task A_manual_incident_from_a_trace_search_is_one_incident_per_trace_with_a_ticket_draft()
    {
        var first = await _rig.Actions.OpenManualAsync(new ManualIncidentRequest("ann", Rig.Trace, null, null, "customer call from jane.doe@example.com"));
        var again = await _rig.Actions.OpenManualAsync(new ManualIncidentRequest("ann", Rig.Trace, null, null, null));

        Assert.That(again.Id, Is.EqualTo(first.Id));
        AssertValid(first);
        Assert.That((first.Trigger, first.Severity, first.VerdictSource), Is.EqualTo(("manual", "Warning", "none")));
        Assert.That(first.Report["correlation"]!["trace_ids"]![0]!.GetValue<string>(), Is.EqualTo(Rig.Trace));
        Assert.That(first.ReportJson, Does.Not.Contain("jane.doe@example.com"));
        var draft = (await _rig.Store.NotificationsAsync(first.Id)).Single();
        Assert.That((draft.Channel, draft.Kind), Is.EqualTo(("ticket_webhook", "draft")), draft.Error);
        Assert.That(_rig.Webhook.Requests, Is.Empty, "no rule verdict: never an automatic ticket");
    }

    [Test]
    public async Task A_manual_incident_from_an_entity_search_correlates_the_entity()
    {
        var row = await _rig.Actions.OpenManualAsync(new ManualIncidentRequest("ann", null, "organization", Rig.OrgId, null));
        AssertValid(row);
        Assert.That(row.Report["correlation"]!["entities"]![0]!["entity"]!.GetValue<string>(), Is.EqualTo("organization"));
        Assert.That(row.Report["evidence"]!.AsArray().Select(e => e!["source"]!.GetValue<string>()), Does.Contain("audit_trail_by_entity"));
    }

    // ---- restart ----

    [Test]
    public async Task A_restart_keeps_the_open_incident_so_the_same_fingerprint_is_not_notified_again()
    {
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error));
        Assert.That((_rig.Email.Sent.Count, _rig.Webhook.Requests.Count), Is.EqualTo((1, 1)));
        var dir = Path.GetDirectoryName(Path.GetDirectoryName(_rig.DbPath))!;
        var record = _rig.Keyed("task.create.unavailable", RecordLevel.Error, message: "after the restart", at: _rig.Time.GetUtcNow().AddMinutes(3));

        using var restarted = _rig.Restart();
        var results = await restarted.FeedAsync(record);

        Assert.That(results.Single().Escalation!.Action, Is.EqualTo(EscalationAction.AppendToOpen), "the analyser finds the open incident in the store");
        var row = (await restarted.AllIncidentsAsync()).Single();
        Assert.That(row.Count, Is.EqualTo(2));
        Assert.That((restarted.Email.Sent.Count, restarted.Webhook.Requests.Count), Is.EqualTo((0, 0)), "no second action for the fingerprint");
        Assert.That(dir, Is.Not.Empty);
    }

    [Test]
    public async Task A_record_already_seen_before_a_restart_is_dropped_as_a_duplicate()
    {
        var record = _rig.Keyed("task.create.unavailable", RecordLevel.Error);
        await _rig.FeedAsync(record);
        using var restarted = _rig.Restart();
        Assert.That(await restarted.FeedAsync(record), Is.Empty);
        Assert.That((await restarted.AllIncidentsAsync()).Single().Count, Is.EqualTo(1));
    }
}
