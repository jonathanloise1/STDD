using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyApp.Monitor.Hosting;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Store;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

/// <summary>What the host does on its timer: daily retention at 03:00, suppression summaries, the digest.</summary>
public class MaintenanceTests
{
    private Rig _rig = null!;

    [SetUp]
    public void SetUp() => _rig = new Rig(start: new DateTimeOffset(2026, 11, 6, 2, 0, 0, TimeSpan.Zero));

    [TearDown]
    public void TearDown() => _rig.Dispose();

    private MonitorHost Host => _rig.Services.GetServices<IHostedService>().OfType<MonitorHost>().Single();

    private async Task<string> ClosedOldIncidentAsync(string id, string fingerprint)
    {
        var old = _rig.Time.GetUtcNow().AddDays(-100);
        var report = (JsonObject)JsonNode.Parse(ReportJson())!;
        report["id"] = id;
        report["fingerprint"] = fingerprint;
        report["status"] = "closed";
        report["outcome"] = new JsonObject { ["value"] = "fixed", ["ticket"] = null, ["pull_request"] = null, ["closed_at"] = SqliteDatabase.Iso(old), ["closed_by"] = "ann", ["note"] = null };
        await _rig.Store.CreateAsync(report, "rule", old, []);
        return id;
    }

    private static string ReportJson()
    {
        using var stream = typeof(MonitorApp).Assembly.GetManifestResourceStream("incident-report.example.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Test]
    public async Task Retention_runs_once_a_day_from_03_00_local_time_and_removes_what_is_older_than_the_retention_days()
    {
        await ClosedOldIncidentAsync("INC-20260801-0001", "aaaaaaaaaaaaaaaa");

        await Host.MaintenanceTickAsync(null, default);   // 02:00
        Assert.That(await _rig.Store.GetAsync("INC-20260801-0001"), Is.Not.Null, "not yet 03:00");

        _rig.Time.Set(new DateTimeOffset(2026, 11, 6, 3, 5, 0, TimeSpan.Zero));
        await Host.MaintenanceTickAsync(null, default);
        Assert.That(await _rig.Store.GetAsync("INC-20260801-0001"), Is.Null, "older than 90 days: removed");

        await ClosedOldIncidentAsync("INC-20260801-0002", "bbbbbbbbbbbbbbbb");
        _rig.Time.Advance(TimeSpan.FromHours(1));
        await Host.MaintenanceTickAsync(null, default);
        Assert.That(await _rig.Store.GetAsync("INC-20260801-0002"), Is.Not.Null, "once a day");

        _rig.Time.Advance(TimeSpan.FromDays(1));
        await Host.MaintenanceTickAsync(null, default);
        Assert.That(await _rig.Store.GetAsync("INC-20260801-0002"), Is.Null, "the next day");
    }

    [Test]
    public async Task The_timer_flushes_ended_suppression_windows_and_runs_the_digest()
    {
        var now = _rig.Time.GetUtcNow();
        await _rig.Actions.AddWindowAsync("short", now.AddMinutes(-1), now.AddMinutes(10), "task.*", null, "ann");
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error));
        Assert.That(await _rig.Store.EventsAsync("window:short"), Is.Empty, "the window is still open");

        _rig.Time.Advance(TimeSpan.FromMinutes(11));
        await Host.MaintenanceTickAsync(null, default);
        Assert.That((await _rig.Store.EventsAsync("window:short")).Single().Kind, Is.EqualTo("suppression_summary"));
    }
}

public class NotificationSettingsTests
{
    [Test]
    public void The_notifications_section_of_the_profile_is_read_with_its_defaults()
    {
        var node = JsonNode.Parse("""
            {
              "email": { "enabled": "true", "smtp_host": "mail.test", "smtp_port": 587, "use_tls": true, "from": "m@test", "to": ["a@test", "b@test"],
                         "username_setting": "U", "password_setting": "P", "template": "templates/email-incident.txt" },
              "ticket_webhook": { "enabled": true, "url": "http://t.test", "method": "post", "headers": { "Authorization": "Bearer x", "X-Empty": "" }, "template": "templates/ticket-incident.json", "id_path": "ticket.id" }
            }
            """);
        var s = NotificationSettings.From(node);
        Assert.That((s.Email.Enabled, s.Email.SmtpHost, s.Email.SmtpPort, s.Email.UseTls, s.Email.To.Count), Is.EqualTo((true, "mail.test", 587, true, 2)));
        Assert.That((s.Webhook.Enabled, s.Webhook.Method, s.Webhook.IdPath), Is.EqualTo((true, "POST", "ticket.id")));
        Assert.That(s.Webhook.Headers.Keys, Is.EqualTo(new[] { "Authorization" }), "a header whose setting is empty is not sent");
    }

    [Test]
    public void A_webhook_without_a_url_is_disabled_whatever_its_flag_says_and_a_missing_section_means_everything_off()
    {
        Assert.That(NotificationSettings.From(JsonNode.Parse("""{ "ticket_webhook": { "enabled": true, "url": "" } }""")).Webhook.Enabled, Is.False);
        var none = NotificationSettings.From(null);
        Assert.That((none.Email.Enabled, none.Webhook.Enabled), Is.EqualTo((false, false)));
    }

    [TestCase("http://localhost:5310", "http://localhost:5310")]
    [TestCase("http://+:8080", "http://localhost:8080")]
    [TestCase("http://*:8080/", "http://localhost:8080")]
    [TestCase("http://0.0.0.0:8080;http://127.0.0.1:9", "http://localhost:8080")]
    [TestCase("https://monitor.example.test", "https://monitor.example.test")]
    [TestCase("", "http://localhost:5310")]
    [TestCase(null, "http://localhost:5310")]
    public void The_link_in_a_notification_uses_the_first_dashboard_url(string? urls, string expected) =>
        Assert.That(DashboardAddress.FromUrls(urls), Is.EqualTo(expected));
}
