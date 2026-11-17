using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MyApp.Monitor.Store;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

/// <summary>
/// The whole loop inside one process (operations.md 2): fixture log files in the on-premises source, through the analyser, the rules and the
/// gates, to the store, to a fake notification, and the dashboard pages that show the incident, with the planted e-mail address nowhere.
/// </summary>
public class EndToEndTests
{
    private static readonly string[] FixtureLines = BuildFixture();

    private static string[] BuildFixture()
    {
        var lines = new List<string> { RunHarness.UnavailableLine() };
        lines.AddRange(Enumerable.Range(0, 16).Select(RunHarness.AccessLostLine));
        lines.Add(RunHarness.UnknownLine());
        return [.. lines];
    }

    private static string Everything(RunHarness h)
    {
        var sb = new StringBuilder();
        foreach (var email in h.Email.Sent) sb.AppendLine(email.Message.Subject).AppendLine(email.Message.Body);
        foreach (var hook in h.Webhook.Requests) sb.AppendLine(hook.Body);
        return sb.ToString();
    }

    [Test]
    public async Task Fixture_logs_become_incidents_notifications_and_dashboard_pages_and_the_planted_address_reaches_none_of_them()
    {
        await using var h = new RunHarness();
        h.WriteLog("myapp-api-20261106.jsonl", FixtureLines);
        await h.StartAsync();
        await h.WaitForIncidentsAsync(3);
        await RunHarness.WaitForAsync(() => Task.FromResult(h.Email.Sent.Count >= 2 && h.Webhook.Requests.Count >= 1), "the notifications");

        // ---- the store: three incidents, each with a schema-valid report ----
        var incidents = (await h.Info.Store.ListAsync(new IncidentFilter())).OrderBy(i => i.Id).ToList();
        Assert.That(incidents.Select(i => (i.Trigger, i.Severity)), Is.EquivalentTo(new[] { ("severity", "Error"), ("rate", "Warning"), ("unknown", "Error") }));
        var unavailable = incidents.Single(i => i.Trigger == "severity");
        var rate = incidents.Single(i => i.Trigger == "rate");
        var unknown = incidents.Single(i => i.Trigger == "unknown");
        var schema = new MyApp.Monitor.Tools.SchemaSubsetValidator(JsonNode.Parse(Fx.Support("incident-report.schema.json"))!);
        foreach (var i in incidents) Assert.That(schema.IsValid(i.Report, out var errors), Is.True, i.Id + ": " + string.Join("; ", errors));
        Assert.That(unavailable.Report["signature"]!["log_key"]!.GetValue<string>(), Is.EqualTo("task.create.unavailable"));
        Assert.That(unavailable.Report["evidence"]![0]!["reference"]!.GetValue<string>(), Does.Contain("myapp-api-20261106.jsonl"));
        Assert.That(unavailable.Report["evidence"]![0]!["excerpt"]!.GetValue<string>(), Does.Contain("[email]"));
        Assert.That(rate.Count, Is.GreaterThanOrEqualTo(16));
        Assert.That(rate.VerdictSource, Is.EqualTo("rule"));
        Assert.That(unknown.VerdictSource, Is.EqualTo("none"));

        // ---- the gates: ticket and e-mail for the matched error, drafts for the rest ----
        Assert.That(h.Webhook.Requests.Select(r => r.IdempotencyKey), Is.EqualTo(new[] { $"{unavailable.Id}:created" }));
        Assert.That(h.Email.Sent.Select(e => e.Message.Subject).Count(s => s.Contains(unavailable.Id)), Is.EqualTo(1));
        Assert.That(h.Email.Sent.Select(e => e.Message.Subject).Count(s => s.Contains(unknown.Id)), Is.EqualTo(1));
        var drafts = await h.Info.Store.NotificationsAsync(kind: "draft");
        Assert.That(drafts.Select(d => d.IncidentId), Is.EquivalentTo(new[] { rate.Id, unknown.Id }));
        Assert.That(await h.Info.Store.NotificationsAsync(kind: "failed"), Is.Empty);

        // ---- the dashboard pages ----
        await RunHarness.WaitForAsync(async () => (await h.Info.Store.AllOffsetsAsync()).Any(o => o.Offset > 0), "the committed offset");
        var index = await h.GetAsync("/");
        Assert.That(index, Does.Contain(unavailable.Id).And.Contain(rate.Id).And.Contain(unknown.Id));
        var page = await h.GetAsync($"/incidents/{unavailable.Id}");
        Assert.That(page, Does.Contain("Rule verdict").And.Contain("Model verdict").And.Contain("[email]").And.Contain("task.create.unavailable").And.Contain("Acknowledge"));
        Assert.That(await h.GetAsync($"/incidents/{unknown.Id}"), Does.Contain("&lt;script&gt;").And.Not.Contain("<script>alert"), "page output is encoded");
        var draftsPage = await h.GetAsync("/drafts");
        Assert.That(draftsPage, Does.Contain(rate.Id).And.Contain(unknown.Id).And.Contain("Send draft"));
        var signals = await h.GetAsync("/signals");
        Assert.That(signals, Does.Contain("task.create.access_lost").And.Contain("t-"));
        var sources = await h.GetAsync("/sources");
        Assert.That(System.Net.WebUtility.HtmlDecode(sources), Does.Contain("applogs").And.Contain("1.4.0+abc1234").And.Contain("myapp-api-20261106.jsonl"));
        await h.GetAsync("/review");
        var trace = await h.GetAsync($"/search?trace={Rig.Trace}");
        Assert.That(trace, Does.Contain("logs_by_correlation").And.Contain("[email]"));

        var pages = new StringBuilder(index).Append(page).Append(draftsPage).Append(signals).Append(sources).Append(trace);
        foreach (var i in incidents) pages.Append(await h.GetAsync($"/incidents/{i.Id}"));

        Assert.That(await h.StopAsync(), Is.EqualTo(0));

        // ---- nothing planted anywhere: the notifications, the pages, the store files ----
        Assert.That(Everything(h), Does.Not.Contain(RunHarness.Planted));
        Assert.That(pages.ToString(), Does.Not.Contain(RunHarness.Planted));
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(h.DbPath)!))
            Assert.That(Encoding.UTF8.GetString(File.ReadAllBytes(file)), Does.Not.Contain(RunHarness.Planted), file);
        Assert.That(Everything(h), Does.Contain("[email]"), "masked, not removed");
    }

    [Test]
    public async Task A_second_run_over_the_same_files_and_store_changes_nothing_and_new_records_of_the_same_fingerprints_fold_into_the_open_incidents()
    {
        await using var first = new RunHarness();
        first.WriteLog("myapp-api-20261106.jsonl", FixtureLines);
        await first.StartAsync();
        await first.WaitForIncidentsAsync(3);
        await RunHarness.WaitForAsync(() => Task.FromResult(first.Email.Sent.Count >= 2 && first.Webhook.Requests.Count >= 1), "the notifications");
        // the file has been read to its end and the offset committed
        await RunHarness.WaitForAsync(async () => (await first.Info.Store.AllOffsetsAsync()).Any(o => o.Offset > 0), "a saved offset");
        await Task.Delay(1500);
        Assert.That(await first.StopAsync(), Is.EqualTo(0));

        var before = (await ReadAll(first)).ToDictionary(i => i.Id);
        var counts = (Email: first.Email.Sent.Count, Hooks: first.Webhook.Requests.Count);
        var offsets = new FileInfo(Path.Combine(first.LogDirectory, "myapp-api-20261106.jsonl")).Length;

        // ---- second run: nothing is read twice ----
        await using var second = first.Next();
        await second.StartAsync();
        await Task.Delay(2500);   // several polls
        Assert.That((await second.Info.Store.ListAsync(new IncidentFilter())).Count, Is.EqualTo(3), "no incident is opened twice");
        Assert.That((second.Email.Sent.Count, second.Webhook.Requests.Count), Is.EqualTo(counts), "no second action for any fingerprint");
        Assert.That((await second.Info.Store.AllOffsetsAsync()).Single().Offset, Is.EqualTo(offsets), "the offset persisted across the restart");

        // ---- new records of the same fingerprints: dedup against the stored incidents ----
        second.AppendLog("myapp-api-20261106.jsonl",
            RunHarness.UnavailableLine("2026-11-06T11:00:00.000Z", "again after the restart"),
            RunHarness.UnknownLine("2026-11-06T11:01:00.000Z"));
        var unavailableId = before.Values.Single(i => i.Trigger == "severity").Id;
        await RunHarness.WaitForAsync(async () => (await second.Info.Store.GetAsync(unavailableId))!.Count >= 2, "the appended record");
        await Task.Delay(1000);

        var after = (await ReadAll(second)).ToDictionary(i => i.Id);
        Assert.That(after.Keys, Is.EquivalentTo(before.Keys), "still three incidents");
        Assert.That(after[unavailableId].Count, Is.EqualTo(2));
        Assert.That(after.Values.Single(i => i.Trigger == "unknown").Count, Is.EqualTo(2));
        Assert.That((second.Email.Sent.Count, second.Webhook.Requests.Count), Is.EqualTo(counts), "folded into the open incidents: no new notification");
        Assert.That(await second.StopAsync(), Is.EqualTo(0));
    }

    private static async Task<IReadOnlyList<IncidentRow>> ReadAll(RunHarness h)
    {
        var store = new SqliteStore(SqliteDatabase.Open(h.DbPath));
        try { return await store.ListAsync(new IncidentFilter()); }
        finally { store.Database.Close(); }
    }
}
