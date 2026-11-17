using System.Text.Json.Nodes;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Pack;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Unit;

public class GatesTests
{
    private static Gates Default() => new(new GateSettings());

    private static IncidentFacts Facts(string severity = "Error", string trigger = "severity", string kind = "keyed", bool rule = true, bool model = false) =>
        new(severity, trigger, kind, rule, model);

    [TestCase("Error", "severity", "keyed", true, true)]
    [TestCase("Critical", "severity", "template", true, true)]
    [TestCase("Error", "regression", "keyed", true, true)]
    [TestCase("Error", "health", "template", true, true)]
    [TestCase("Warning", "severity", "keyed", true, false)]     // below the gate
    [TestCase("Error", "rate", "keyed", true, false)]            // a residual trigger
    [TestCase("Error", "unknown", "none", false, false)]
    [TestCase("Error", "manual", "none", false, false)]
    [TestCase("Error", "noise_out_of_context", "keyed", true, false)]
    [TestCase("Error", "severity", "keyed", false, false)]       // no rule verdict
    [TestCase("Error", "severity", "none", true, false)]         // no matched signature
    public void A_ticket_is_automatic_only_from_a_rule_verdict_on_a_matched_signature_with_the_right_trigger_and_severity(
        string severity, string trigger, string kind, bool rule, bool automatic) =>
        Assert.That(Default().TicketIsAutomatic(Facts(severity, trigger, kind, rule)), Is.EqualTo(automatic));

    [Test]
    public void A_model_verdict_alone_never_makes_a_ticket_automatic()
    {
        Assert.That(Default().TicketIsAutomatic(Facts(rule: false, model: true)), Is.False);
        var permissive = new Gates(new GateSettings { TicketAllowModelVerdict = true });
        Assert.That(permissive.TicketIsAutomatic(Facts(rule: false, model: true)), Is.False, "the setting exists but stays false in version 1");
    }

    [Test]
    public void The_ticket_gate_follows_its_configured_minimum_severity()
    {
        var gates = new Gates(new GateSettings { TicketMinSeverity = "Critical" });
        Assert.That(gates.TicketIsAutomatic(Facts("Error")), Is.False);
        Assert.That(gates.TicketIsAutomatic(Facts("Critical")), Is.True);
    }

    [Test]
    public void Creation_plans_a_ticket_decision_and_an_e_mail_at_or_above_the_gate()
    {
        var plan = Default().Plan(NotificationEvents.Created, Facts());
        Assert.That(plan.Select(p => (p.Channel, p.Draft)), Is.EqualTo(new[] { ("ticket_webhook", false), ("email", false) }));

        var warning = Default().Plan(NotificationEvents.Created, Facts("Warning"));
        Assert.That(warning.Select(p => (p.Channel, p.Draft)), Is.EqualTo(new[] { ("ticket_webhook", true) }), "a draft, and no e-mail below the gate (the digest covers Warning)");

        var unknown = Default().Plan(NotificationEvents.Created, Facts(trigger: "unknown", kind: "none", rule: false));
        Assert.That(unknown.Select(p => (p.Channel, p.Draft)), Is.EqualTo(new[] { ("ticket_webhook", true), ("email", false) }));
    }

    [Test]
    public void A_severity_raise_plans_only_an_e_mail_and_only_when_the_new_severity_passes_the_gate()
    {
        Assert.That(Default().Plan(NotificationEvents.SeverityRaised, Facts("Error")).Select(p => p.Channel), Is.EqualTo(new[] { "email" }));
        Assert.That(Default().Plan(NotificationEvents.SeverityRaised, Facts("Warning")), Is.Empty);
    }

    [Test]
    public void The_e_mail_gate_follows_its_configured_minimum_severity()
    {
        var gates = new Gates(new GateSettings { EmailMinSeverity = "Warning" });
        Assert.That(gates.Plan(NotificationEvents.Created, Facts("Warning")).Select(p => p.Channel), Is.EqualTo(new[] { "ticket_webhook", "email" }));
    }
}

public class SuppressionWindowTests
{
    private static readonly DateTimeOffset Monday10 = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);   // a Monday

    [TestCase("task.*", "task.create.unavailable", true)]
    [TestCase("task.*", "billing.invoice.x", false)]
    [TestCase("*", "anything", true)]
    [TestCase("t-*", "t-2bb5dcdc1949", true)]
    [TestCase("t-?bb5dcdc1949", "t-2bb5dcdc1949", true)]
    [TestCase("task.create.unavailable", "task.create.unavailable", true)]
    [TestCase("task.create", "task.create.unavailable", false)]
    public void The_match_is_a_glob_over_signature_or_template_ids(string pattern, string id, bool matches) =>
        Assert.That(new SuppressionWindow("w", pattern, null, null, null, null, null, "profile").Matches(id), Is.EqualTo(matches));

    [Test]
    public void A_fixed_window_is_active_from_its_start_until_its_end()
    {
        var w = new SuppressionWindow("w", "*", Monday10, Monday10.AddHours(2), null, null, null, "profile");
        Assert.That(w.ActiveInstance(Monday10.AddMinutes(-1), TimeZoneInfo.Utc), Is.Null);
        Assert.That(w.ActiveInstance(Monday10, TimeZoneInfo.Utc), Is.EqualTo((Monday10, Monday10.AddHours(2))));
        Assert.That(w.ActiveInstance(Monday10.AddHours(2), TimeZoneInfo.Utc), Is.Null);
    }

    [Test]
    public void A_weekly_window_is_active_on_its_day_between_from_and_to_in_the_given_time_zone()
    {
        var w = new SuppressionWindow("w", "*", null, null, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0), "profile");
        Assert.That(w.ActiveInstance(Monday10, TimeZoneInfo.Utc), Is.Not.Null);
        Assert.That(w.ActiveInstance(Monday10.AddHours(2), TimeZoneInfo.Utc), Is.Null);
        Assert.That(w.ActiveInstance(Monday10.AddDays(1), TimeZoneInfo.Utc), Is.Null, "Tuesday");
        var plusTwo = TimeZoneInfo.CreateCustomTimeZone("plus2", TimeSpan.FromHours(2), "plus2", "plus2");
        Assert.That(w.ActiveInstance(Monday10, plusTwo), Is.Null, "10:00 UTC is 12:00 local, outside 09:00 to 11:00");
        Assert.That(w.ActiveInstance(Monday10.AddHours(-2), plusTwo), Is.Not.Null, "08:00 UTC is 10:00 local");
    }

    [Test]
    public void A_weekly_window_across_midnight_is_active_after_midnight_of_the_next_day()
    {
        var w = new SuppressionWindow("w", "*", null, null, DayOfWeek.Sunday, new TimeOnly(23, 0), new TimeOnly(1, 0), "profile");
        var sunday2330 = new DateTimeOffset(2026, 11, 1, 23, 30, 0, TimeSpan.Zero);
        Assert.That(w.ActiveInstance(sunday2330, TimeZoneInfo.Utc), Is.Not.Null);
        Assert.That(w.ActiveInstance(sunday2330.AddHours(1), TimeZoneInfo.Utc), Is.Not.Null, "00:30 on Monday still belongs to Sunday's window");
        Assert.That(w.ActiveInstance(sunday2330.AddHours(2), TimeZoneInfo.Utc), Is.Null);
    }

    [Test]
    public void Profile_windows_are_read_from_the_suppression_list_and_unreadable_ones_are_skipped()
    {
        var node = JsonNode.Parse("""
            [
              { "name": "deploy", "starts_at": "2026-11-06T08:00:00Z", "ends_at": "2026-11-06T09:00:00Z", "match": "task.*" },
              { "name": "nightly", "weekly": { "day": "Sunday", "from": "02:00", "to": "04:00" }, "match": "t-*" },
              { "name": "broken", "match": "*" }
            ]
            """);
        var windows = SuppressionWindow.FromProfile(node);
        Assert.That(windows.Select(w => w.Name), Is.EqualTo(new[] { "deploy", "nightly" }));
        Assert.That(windows[1].Day, Is.EqualTo(DayOfWeek.Sunday));
        Assert.That(SuppressionWindow.FromProfile(null), Is.Empty);
    }
}
