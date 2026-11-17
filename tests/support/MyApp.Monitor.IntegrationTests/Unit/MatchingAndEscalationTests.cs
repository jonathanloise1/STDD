using MyApp.Monitor.Analysis;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Unit;

public class MatchingTests
{
    [Test]
    public void A_known_key_matches_its_keyed_signature()
    {
        var a = Fx.Analyser([Fx.Sig()]);
        var m = a.Match(Fx.Record(eventName: "task.create.access_lost"));
        Assert.That(m.Kind, Is.EqualTo(MatchKind.Matched));
        Assert.That(m.Signature!.Id, Is.EqualTo("task.create.access_lost"));
    }

    [Test]
    public void An_unknown_key_is_reported_with_the_key()
    {
        var m = Fx.Analyser([Fx.Sig()]).Match(Fx.Record(eventName: "task.create.new_thing"));
        Assert.That(m.Kind, Is.EqualTo(MatchKind.UnknownKey));
        Assert.That(m.Id, Is.EqualTo("task.create.new_thing"));
        Assert.That(m.Signature, Is.Null);
    }

    [Test]
    public void A_record_without_key_matches_a_template_signature_by_template_id()
    {
        var record = Fx.Record(message: "disk is full");
        var id = TemplateExtractor.Extract(record).Id;
        var m = Fx.Analyser([Fx.Sig(id: id, template: true)]).Match(record);
        Assert.That(m.Kind, Is.EqualTo(MatchKind.Matched));
        Assert.That(m.Template!.Id, Is.EqualTo(id));
    }

    [Test]
    public void A_record_without_key_and_without_signature_is_an_unknown_template()
    {
        var m = Fx.Analyser([Fx.Sig()]).Match(Fx.Record(message: "disk is full"));
        Assert.That(m.Kind, Is.EqualTo(MatchKind.UnknownTemplate));
        Assert.That(m.Id, Does.StartWith("t-"));
    }

    [Test]
    public void Platform_events_never_match_a_key_even_when_a_key_signature_has_the_same_name()
    {
        var m = Fx.Analyser([Fx.Sig(id: "platform.job.failed")]).Match(Fx.Record(eventName: "platform.job.failed"));
        Assert.That(m.Kind, Is.EqualTo(MatchKind.UnknownTemplate));
    }

    [Test]
    public void Unknown_templates_are_upserted_with_counts_and_a_sample_trace()
    {
        var sink = new InMemoryUnknownTemplateSink();
        var a = Fx.Analyser([], unknowns: sink);
        a.Analyse(Fx.Record(message: "disk is full", traceId: "4bf92f3577b34da6a3ce929d0e0e4736"));
        a.Analyse(Fx.Record(message: "disk is full", level: RecordLevel.Warning));
        var row = sink.All().Single();
        Assert.That(row.Count, Is.EqualTo(2));
        Assert.That(row.Level, Is.EqualTo("Warning"));
        Assert.That(row.SampleTraceId, Is.EqualTo("4bf92f3577b34da6a3ce929d0e0e4736"));
    }
}

public class EscalationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    private static AnalysisResult Feed(Analyser a, string key, int count, RecordLevel level = RecordLevel.Warning, string? version = null, int everySeconds = 30)
    {
        AnalysisResult? last = null;
        for (var i = 0; i < count; i++)
            last = a.Analyse(Fx.Record(eventName: key, level: level, at: T0.AddSeconds(i * everySeconds), version: version, props: new() { ["OrganizationId"] = "org-1" }));
        return last!;
    }

    // ---- severity ----

    [TestCase("Error")]
    [TestCase("Critical")]
    public void Active_error_and_critical_signatures_escalate_by_severity_with_a_rule_verdict(string severity)
    {
        var a = Fx.Analyser([Fx.Sig(severity: severity)]);
        var r = Feed(a, "task.create.access_lost", 1);
        Assert.That(r.Escalation, Is.Not.Null);
        Assert.That(r.Escalation!.Trigger, Is.EqualTo(Triggers.Severity));
        Assert.That(r.Escalation.Severity, Is.EqualTo(severity));
        Assert.That(r.Escalation.Residual, Is.False);
    }

    [TestCase("Information")]
    [TestCase("Warning")]
    public void Active_information_and_warning_signatures_below_the_threshold_only_count(string severity)
    {
        var a = Fx.Analyser([Fx.Sig(severity: severity, expected: 5)]);
        var r = Feed(a, "task.create.access_lost", 5);
        Assert.That(r.Escalation, Is.Null);
        Assert.That(r.CountedOnlyReason, Is.Not.Null);
    }

    // ---- rate ----

    [Test]
    public void Rate_escalates_above_expected_times_multiplier_with_at_least_the_minimum_count_and_is_residual()
    {
        // expected 1 per hour, multiplier 3: needs more than 3 per hour and at least 5 records
        var a = Fx.Analyser([Fx.Sig(severity: "Warning", expected: 1)]);
        Assert.That(Feed(a, "task.create.access_lost", 4).Escalation, Is.Null, "4 records are under rate_min_count 5");
        var r = a.Analyse(Fx.Record(eventName: "task.create.access_lost", level: RecordLevel.Warning, at: T0.AddSeconds(200)));
        Assert.That(r.SignatureWindowCount, Is.EqualTo(5));
        Assert.That(r.Escalation, Is.Not.Null);
        Assert.That(r.Escalation!.Trigger, Is.EqualTo(Triggers.Rate));
        Assert.That(r.Escalation.Residual, Is.True);
    }

    [Test]
    public void Rate_needs_to_be_above_the_threshold_not_just_the_minimum_count()
    {
        // expected 5 per hour -> threshold 15 per hour: 10 records are not enough
        var a = Fx.Analyser([Fx.Sig(severity: "Warning", expected: 5)]);
        Assert.That(Feed(a, "task.create.access_lost", 15, everySeconds: 20).Escalation, Is.Null, "15 per hour is equal to the threshold, not above it");
        Assert.That(a.Analyse(Fx.Record(eventName: "task.create.access_lost", level: RecordLevel.Warning, at: T0.AddMinutes(10))).Escalation, Is.Not.Null);
    }

    [Test]
    public void An_expected_rate_of_zero_uses_one_as_the_base()
    {
        var a = Fx.Analyser([Fx.Sig(severity: "Information", expected: 0)]);
        Assert.That(Feed(a, "task.create.access_lost", 5).Escalation, Is.Not.Null, "5 per hour is above max(0,1) x 3");
    }

    [Test]
    public void Records_older_than_the_window_leave_the_rate()
    {
        var a = Fx.Analyser([Fx.Sig(severity: "Warning", expected: 1)]);
        Feed(a, "task.create.access_lost", 4, everySeconds: 10);
        var r = a.Analyse(Fx.Record(eventName: "task.create.access_lost", level: RecordLevel.Warning, at: T0.AddHours(3)));
        Assert.That(r.SignatureWindowCount, Is.EqualTo(1));
        Assert.That(r.Escalation, Is.Null);
    }

    [Test]
    public void Rate_is_scaled_to_one_hour_from_the_window()
    {
        var settings = new AnalyserSettings { WindowMinutes = 30 };
        var a = Fx.Analyser([Fx.Sig(severity: "Warning", expected: 100)], settings: settings);
        var r = Feed(a, "task.create.access_lost", 3);
        Assert.That(r.SignatureRatePerHour, Is.EqualTo(6.0).Within(0.001));
    }

    // ---- regression ----

    [TestCase("1.3.9", false)]
    [TestCase("1.4.0", true)]
    [TestCase("1.4.0+abc123", true)]
    [TestCase("2.0.0", true)]
    [TestCase(null, true)]
    [TestCase("not a version", true)]
    [TestCase("1.4.0-rc1", false)]
    public void Fixed_signatures_regress_when_the_record_version_reaches_fixed_in(string? version, bool regression)
    {
        var a = Fx.Analyser([Fx.Sig(severity: "Warning", status: "fixed", fixedIn: "1.4.0")]);
        var r = Feed(a, "task.create.access_lost", 1, version: version);
        Assert.That(r.Escalation is not null, Is.EqualTo(regression));
        if (regression)
        {
            Assert.That(r.Escalation!.Trigger, Is.EqualTo(Triggers.Regression));
            Assert.That(r.Escalation.Residual, Is.False);
        }
    }

    [TestCase("Information", "Warning")]
    [TestCase("Warning", "Error")]
    [TestCase("Error", "Critical")]
    [TestCase("Critical", "Critical")]
    public void A_regression_raises_the_severity_one_step_at_most_critical(string from, string to)
    {
        var a = Fx.Analyser([Fx.Sig(severity: from, status: "fixed", fixedIn: "1.4.0")]);
        Assert.That(Feed(a, "task.create.access_lost", 1, version: "1.5.0").Escalation!.Severity, Is.EqualTo(to));
    }

    // ---- noise ----

    private static NoiseInfo Noise(string expires) => new() { Rationale = "framework", Expires = expires };

    [Test]
    public void Noise_within_its_context_is_counted_and_opens_nothing()
    {
        var a = Fx.Analyser([Fx.Sig(severity: "Error", status: "noise", expected: 100, noise: Noise("2027-01-01"))]);
        var r = Feed(a, "task.create.access_lost", 10);
        Assert.That(r.Escalation, Is.Null);
        Assert.That(r.Match.Kind, Is.EqualTo(MatchKind.Matched));
    }

    [Test]
    public void Noise_above_the_rate_threshold_escalates_out_of_context_as_residual()
    {
        var a = Fx.Analyser([Fx.Sig(severity: "Warning", status: "noise", expected: 1, noise: Noise("2027-01-01"))]);
        var r = Feed(a, "task.create.access_lost", 5);
        Assert.That(r.Escalation!.Trigger, Is.EqualTo(Triggers.NoiseOutOfContext));
        Assert.That(r.Escalation.Residual, Is.True);
    }

    [Test]
    public void Expired_noise_is_treated_as_active_with_at_least_warning()
    {
        var a = Fx.Analyser([Fx.Sig(severity: "Information", status: "noise", expected: 1, noise: Noise("2026-11-05"))]);
        // as active at Warning: counted below the rate threshold
        Assert.That(Feed(a, "task.create.access_lost", 2).Escalation, Is.Null);
        var b = Fx.Analyser([Fx.Sig(severity: "Error", status: "noise", expected: 1, noise: Noise("2026-11-05"))]);
        var r = Feed(b, "task.create.access_lost", 1);
        Assert.That(r.Escalation!.Trigger, Is.EqualTo(Triggers.NoiseExpired));
        Assert.That(r.Escalation.Residual, Is.False, "as active: an Error escalates by severity with a rule verdict");
    }

    [Test]
    public void Noise_that_expires_today_is_still_noise()
    {
        var a = Fx.Analyser([Fx.Sig(severity: "Error", status: "noise", expected: 100, noise: Noise("2026-11-06"))]);
        Assert.That(Feed(a, "task.create.access_lost", 1).Escalation, Is.Null);
    }

    // ---- unknown ----

    [TestCase(RecordLevel.Error)]
    [TestCase(RecordLevel.Critical)]
    public void Unknown_error_and_above_escalate_as_residual(RecordLevel level)
    {
        var a = Fx.Analyser([]);
        var r = Feed(a, "task.new.thing", 1, level);
        Assert.That(r.Match.Kind, Is.EqualTo(MatchKind.UnknownKey));
        Assert.That(r.Escalation!.Trigger, Is.EqualTo(Triggers.Unknown));
        Assert.That(r.Escalation.Residual, Is.True);
    }

    [Test]
    public void Unknown_warning_escalates_at_the_minimum_count_in_the_window()
    {
        var a = Fx.Analyser([]);
        Assert.That(Feed(a, "task.new.thing", 2).Escalation, Is.Null);
        Assert.That(a.Analyse(Fx.Record(eventName: "task.new.thing", level: RecordLevel.Warning, at: T0.AddSeconds(70))).Escalation!.Trigger, Is.EqualTo(Triggers.Unknown));
    }

    [Test]
    public void Unknown_information_is_counted_and_never_escalates_whatever_the_volume()
    {
        var a = Fx.Analyser([]);
        Assert.That(Feed(a, "task.new.thing", 200, RecordLevel.Information, everySeconds: 1).Escalation, Is.Null);
    }

    [Test]
    public void Unknown_template_at_error_escalates_and_is_listed()
    {
        var sink = new InMemoryUnknownTemplateSink();
        var a = Fx.Analyser([], unknowns: sink);
        var r = a.Analyse(Fx.Record(message: "boom", level: RecordLevel.Error));
        Assert.That(r.Match.Kind, Is.EqualTo(MatchKind.UnknownTemplate));
        Assert.That(r.Escalation!.Trigger, Is.EqualTo(Triggers.Unknown));
        Assert.That(sink.All(), Has.Count.EqualTo(1));
    }

    // ---- health ----

    [TestCase("monitor.api.unreachable", RecordLevel.Critical, "Critical")]
    [TestCase("monitor.api.unhealthy", RecordLevel.Error, "Error")]
    [TestCase("platform.health.unhealthy", RecordLevel.Error, "Error")]
    public void Health_events_escalate_with_a_rule_verdict(string name, RecordLevel level, string severity)
    {
        var r = Fx.Analyser([]).Analyse(Fx.Record(eventName: name, level: level, props: new() { ["Check"] = "sqlserver", ["Url"] = "https://x/health" }));
        Assert.That(r.Escalation!.Trigger, Is.EqualTo(Triggers.Health));
        Assert.That(r.Escalation.Severity, Is.EqualTo(severity));
        Assert.That(r.Escalation.Residual, Is.False);
    }

    // ---- counters ----

    [Test]
    public void Every_record_updates_hourly_counters_per_signature_environment_and_fingerprint()
    {
        var counters = new InMemoryCounterStore();
        var a = Fx.Analyser([Fx.Sig(severity: "Information", expected: 1000, fingerprint: ["OrganizationId"])], counters: counters);
        Feed(a, "task.create.access_lost", 3);
        var row = counters.Snapshot().Single();
        Assert.That(row.Count, Is.EqualTo(3));
        Assert.That(row.Environment, Is.EqualTo("prod"));
        Assert.That(row.BucketStart, Is.EqualTo(new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero)));
    }
}

public class FingerprintAndDedupTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public void Fingerprint_is_the_first_16_hex_of_sha256_of_environment_id_and_values()
    {
        Assert.That(Analyser.ComputeFingerprint("prod", "task.create.access_lost", ["0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91"]), Is.EqualTo("e1d74b525b41d7b6"));
        Assert.That(Analyser.ComputeFingerprint("prod", "task.create.access_lost", ["A", "B"]), Is.EqualTo("049adc5402abac7f"));
        Assert.That(Analyser.ComputeFingerprint("prod", "task.create.access_lost", []), Is.EqualTo("67030d80549ba12b"));
    }

    [Test]
    public void The_fingerprint_follows_the_properties_named_by_the_runbook_in_order()
    {
        var a = Fx.Analyser([Fx.Sig(severity: "Error", fingerprint: ["OrganizationId"])]);
        var r = a.Analyse(Fx.Record(eventName: "task.create.access_lost", level: RecordLevel.Warning,
            props: new() { ["OrganizationId"] = "0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91", ["UserId"] = "ignored" }));
        Assert.That(r.Fingerprint, Is.EqualTo("e1d74b525b41d7b6"));
    }

    [Test]
    public void A_different_environment_gives_a_different_fingerprint()
    {
        var a = Fx.Analyser([Fx.Sig(severity: "Error")]);
        var prod = a.Analyse(Fx.Record(eventName: "task.create.access_lost", env: "prod")).Fingerprint;
        var test = a.Analyse(Fx.Record(eventName: "task.create.access_lost", env: "test")).Fingerprint;
        Assert.That(prod, Is.Not.EqualTo(test));
    }

    [Test]
    public void The_first_escalation_opens_and_later_ones_append_while_the_incident_is_open()
    {
        var time = FakeTime.Default();
        var index = new InMemoryIncidentIndex(time);
        var a = Fx.Analyser([Fx.Sig(severity: "Error")], time, index: index);

        var first = a.Analyse(Fx.Record(eventName: "task.create.access_lost", at: T0)).Escalation!;
        Assert.That(first.Action, Is.EqualTo(EscalationAction.OpenNew));
        var (incident, isNew) = index.RecordEscalation(first);
        Assert.That(isNew, Is.True);
        Assert.That(incident.Id, Is.EqualTo("INC-20261106-0001"));

        var second = a.Analyse(Fx.Record(eventName: "task.create.access_lost", at: T0.AddSeconds(5))).Escalation!;
        Assert.That(second.Action, Is.EqualTo(EscalationAction.AppendToOpen));
        Assert.That(second.Open!.Id, Is.EqualTo(incident.Id));
        Assert.That(index.RecordEscalation(second).IsNew, Is.False);
        Assert.That(index.CountOf(incident.Id), Is.EqualTo(2));
    }

    [Test]
    public void A_higher_severity_on_an_open_incident_is_a_severity_raise()
    {
        var time = FakeTime.Default();
        var index = new InMemoryIncidentIndex(time);
        var a = Fx.Analyser([Fx.Sig(severity: "Warning", status: "fixed", fixedIn: "1.0.0"), Fx.Sig(id: "task.new.other", severity: "Error")], time, index: index);
        // same fingerprint cannot span two signatures: use an unknown that gets raised by the same signature id instead
        var e1 = a.Analyse(Fx.Record(eventName: "task.create.access_lost", version: "1.0.0", at: T0)).Escalation!;
        Assert.That(e1.Severity, Is.EqualTo("Error"));
        index.RecordEscalation(e1);
        var open = index.FindOpen(e1.Fingerprint)!;
        Assert.That(open.Severity, Is.EqualTo("Error"));
        var raised = e1 with { Severity = "Critical", Open = open, Action = EscalationAction.RaiseSeverity };
        Assert.That(index.RecordEscalation(raised).Incident.Severity, Is.EqualTo("Critical"));
    }

    [Test]
    public void After_close_a_new_record_opens_a_new_incident_linked_to_the_previous_one()
    {
        var time = FakeTime.Default();
        var index = new InMemoryIncidentIndex(time);
        var a = Fx.Analyser([Fx.Sig(severity: "Error")], time, index: index);
        var first = index.RecordEscalation(a.Analyse(Fx.Record(eventName: "task.create.access_lost", at: T0)).Escalation!).Incident;
        index.Close(first.Id, "fixed");

        var again = a.Analyse(Fx.Record(eventName: "task.create.access_lost", at: T0.AddMinutes(5))).Escalation!;
        Assert.That(again.Action, Is.EqualTo(EscalationAction.OpenNew));
        Assert.That(again.PreviousIncidentId, Is.EqualTo(first.Id));
    }

    [TestCase("noise")]
    [TestCase("duplicate")]
    public void After_a_noise_or_duplicate_outcome_the_new_incident_has_no_previous_link(string outcome)
    {
        var time = FakeTime.Default();
        var index = new InMemoryIncidentIndex(time);
        var a = Fx.Analyser([Fx.Sig(severity: "Error")], time, index: index);
        var first = index.RecordEscalation(a.Analyse(Fx.Record(eventName: "task.create.access_lost", at: T0)).Escalation!).Incident;
        index.Close(first.Id, outcome);
        var again = a.Analyse(Fx.Record(eventName: "task.create.access_lost", at: T0.AddMinutes(5))).Escalation!;
        Assert.That(again.Action, Is.EqualTo(EscalationAction.OpenNew));
        Assert.That(again.PreviousIncidentId, Is.Null);
    }

    [Test]
    public void Different_fingerprints_open_different_incidents()
    {
        var time = FakeTime.Default();
        var index = new InMemoryIncidentIndex(time);
        var a = Fx.Analyser([Fx.Sig(severity: "Error", fingerprint: ["OrganizationId"])], time, index: index);
        var e1 = a.Analyse(Fx.Record(eventName: "task.create.access_lost", props: new() { ["OrganizationId"] = "o1" })).Escalation!;
        index.RecordEscalation(e1);
        var e2 = a.Analyse(Fx.Record(eventName: "task.create.access_lost", props: new() { ["OrganizationId"] = "o2" })).Escalation!;
        Assert.That(e2.Action, Is.EqualTo(EscalationAction.OpenNew));
    }

    [Test]
    public void The_pipeline_drops_duplicate_records_and_masks_before_analysis()
    {
        var time = FakeTime.Default();
        var state = new MyApp.Monitor.Sources.InMemorySourceStateStore();
        var analyser = Fx.Analyser([], time);
        var pipeline = new AnalysisPipeline(Fx.Masker(), analyser, state, time);
        var record = Fx.Record(message: "contact john.doe@example.com now", level: RecordLevel.Error);
        var batch = new SignalBatch("src", [record, record], null);
        var results = pipeline.ProcessAsync(batch, CancellationToken.None).GetAwaiter().GetResult();
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Record.Message, Is.EqualTo("contact [email] now"));
        Assert.That(results[0].Template!.Discriminator, Is.EqualTo("contact [email] now"));
    }
}

public class SemanticVersionTests
{
    [TestCase("1.4.0", "1.4.0", true)]
    [TestCase("1.4.1", "1.4.0", true)]
    [TestCase("1.3.9", "1.4.0", false)]
    [TestCase("10.0.0", "9.9.9", true)]
    [TestCase("1.4", "1.4.0", true)]
    [TestCase("1.4.0-beta", "1.4.0", false)]
    [TestCase("1.5.0-beta", "1.4.0", true)]
    [TestCase("1.4.0+abc", "1.4.0", true)]
    [TestCase("v1.4.0", "1.4.0", true)]
    [TestCase(null, "1.4.0", true)]
    [TestCase("garbage", "1.4.0", true)]
    public void Comparison(string? actual, string fixedIn, bool atLeast) =>
        Assert.That(SemanticVersion.IsAtLeast(actual, fixedIn), Is.EqualTo(atLeast));
}
