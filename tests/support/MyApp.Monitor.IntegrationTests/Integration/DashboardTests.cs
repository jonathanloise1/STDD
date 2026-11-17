using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using MyApp.Monitor.Dashboard;
using MyApp.Monitor.Store;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

/// <summary>The dashboard (operations.md 10.6): every page, its routes and actions, the JSON API, and who can reach it.</summary>
[TestFixture]
public class DashboardTests
{
    private RunHarness _h = null!;
    private IncidentRow _unavailable = null!;
    private IncidentRow _unknown = null!;

    [SetUp]
    public async Task SetUp()
    {
        _h = new RunHarness();
        _h.WriteLog("myapp-api-20261106.jsonl", RunHarness.UnavailableLine(), RunHarness.UnknownLine());
        await _h.StartAsync();
        await _h.WaitForIncidentsAsync(2);
        var rows = await _h.Info.Store.ListAsync(new IncidentFilter());
        _unavailable = rows.Single(r => r.Trigger == "severity");
        _unknown = rows.Single(r => r.Trigger == "unknown");
    }

    [TearDown]
    public async Task TearDown() => await _h.DisposeAsync();

    private static string Decode(string html) => WebUtility.HtmlDecode(html);

    // ---- open incidents ----

    [Test]
    public async Task The_open_incidents_page_lists_incidents_with_counts_per_status_and_filters()
    {
        var page = Decode(await _h.GetAsync("/"));
        Assert.That(page, Does.Contain("Open incidents").And.Contain(_unavailable.Id).And.Contain(_unknown.Id));
        Assert.That(page, Does.Contain("<strong>2</strong> <span class=\"muted\">open</span>"));

        var errors = Decode(await _h.GetAsync("/?severity=Error"));
        Assert.That(errors, Does.Contain(_unavailable.Id));
        var warnings = Decode(await _h.GetAsync("/?severity=Warning"));
        Assert.That(warnings, Does.Not.Contain(_unavailable.Id).And.Contain("No incidents match."));
        Assert.That(Decode(await _h.GetAsync("/?trigger=unknown")), Does.Contain(_unknown.Id).And.Not.Contain(_unavailable.Id));
        Assert.That(Decode(await _h.GetAsync("/?verdict=none")), Does.Contain(_unknown.Id).And.Not.Contain(_unavailable.Id));
        Assert.That(Decode(await _h.GetAsync("/?area=tasks")), Does.Contain(_unavailable.Id).And.Not.Contain(_unknown.Id));
    }

    [Test]
    public async Task Every_page_the_specification_lists_answers_on_its_route()
    {
        foreach (var route in new[] { "/", "/drafts", "/search", "/signals", "/review", "/sources", $"/incidents/{_unavailable.Id}" })
            Assert.That(await _h.GetAsync(route), Does.Contain("<title>"), route);
        using var missing = await _h.Client.GetAsync("/incidents/INC-20990101-0001");
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // ---- the incident page ----

    [Test]
    public async Task The_incident_page_shows_the_report_evidence_both_verdicts_side_by_side_and_the_notifications()
    {
        var page = Decode(await _h.GetAsync($"/incidents/{_unavailable.Id}"));
        Assert.That(page, Does.Contain(_unavailable.Id).And.Contain("task.create.unavailable").And.Contain("US-TASK-01").And.Contain("TASK-CREATE-008"));
        Assert.That(page, Does.Contain("Rule verdict").And.Contain("Model verdict"));
        Assert.That(page.IndexOf("Rule verdict", StringComparison.Ordinal), Is.LessThan(page.IndexOf("Model verdict", StringComparison.Ordinal)));
        Assert.That(page, Does.Contain("The task could not be saved because a dependency failed"));
        Assert.That(page, Does.Contain("Evidence").And.Contain("E1").And.Contain("signal").And.Contain("myapp-api-20261106.jsonl"));
        Assert.That(page, Does.Contain("[email]"));
        Assert.That(page, Does.Contain("Acknowledge").And.Contain("Close with outcome").And.Contain("Investigate again"));
        Assert.That(page, Does.Contain("ticket_webhook").And.Contain("email").And.Contain("sent"));
        Assert.That(page, Does.Contain("Tool calls").And.Contain("logs_by_correlation"));
        Assert.That(page, Does.Contain($"/api/incidents/{_unavailable.Id}?download=true"));
        Assert.That(page, Does.Contain("The investigator did not run").Or.Contain("Investigation skipped").Or.Contain("None."), "the model side says why it is empty");
    }

    [Test]
    public async Task A_residual_incident_shows_that_its_investigation_was_skipped_and_it_keeps_the_rule_verdict()
    {
        var page = Decode(await _h.GetAsync($"/incidents/{_unknown.Id}"));
        Assert.That(page, Does.Contain("None: no signature matched."));
        Assert.That(page, Does.Contain("Investigation skipped"));
    }

    [Test]
    public async Task Acknowledge_moves_the_incident_to_acknowledged_and_records_who_and_when()
    {
        var response = await _h.PostFormAsync($"/incidents/{_unavailable.Id}", $"/incidents/{_unavailable.Id}?handler=Acknowledge", []);
        var page = Decode(await response.Content.ReadAsStringAsync());
        Assert.That(page, Does.Contain("Incident acknowledged."));
        var row = (await _h.Info.Store.GetAsync(_unavailable.Id))!;
        Assert.That(row.Status, Is.EqualTo("acknowledged"));
        Assert.That(row.Report["actions_taken"]!.AsArray().Last()!["action"]!.GetValue<string>(), Is.EqualTo("acknowledged"));
        Assert.That(row.Report["actions_taken"]!.AsArray().Last()!["reference"]!.GetValue<string>(), Is.EqualTo(Environment.UserName));
        Assert.That((await _h.Info.Store.EventsAsync(row.Id)).Select(e => e.Kind), Does.Contain("status_change"));
        Assert.That(Decode(await _h.GetAsync("/")), Does.Contain("<strong>1</strong> <span class=\"muted\">acknowledged</span>"));
    }

    [Test]
    public async Task Close_with_an_outcome_writes_what_the_return_loop_needs_and_the_review_page_lists_it()
    {
        var response = await _h.PostFormAsync($"/incidents/{_unavailable.Id}", $"/incidents/{_unavailable.Id}?handler=Close", new()
        {
            ["value"] = "fixed", ["ticket"] = "https://tickets.example.test/T-1", ["pullRequest"] = "", ["note"] = "Fixed by retrying; contact jane.doe@example.com",
        });
        Assert.That(Decode(await response.Content.ReadAsStringAsync()), Does.Contain("Incident closed as fixed."));

        var row = (await _h.Info.Store.GetAsync(_unavailable.Id))!;
        Assert.That((row.Status, row.Outcome), Is.EqualTo(("closed", "fixed")));
        Assert.That(row.ClosedAt, Is.Not.Null);
        var outcome = row.Report["outcome"]!;
        Assert.That(outcome["value"]!.GetValue<string>(), Is.EqualTo("fixed"));
        Assert.That(outcome["ticket"]!.GetValue<string>(), Is.EqualTo("https://tickets.example.test/T-1"));
        Assert.That(outcome["pull_request"], Is.Null);
        Assert.That(outcome["closed_by"]!.GetValue<string>(), Is.EqualTo(Environment.UserName));
        Assert.That(outcome["note"]!.GetValue<string>(), Is.EqualTo("Fixed by retrying; contact [email]"), "free text is masked");

        var review = Decode(await _h.GetAsync("/review"));
        Assert.That(review, Does.Contain(_unavailable.Id).And.Contain("The test case that catches the failure").And.Contain("pending").And.Contain($"/api/incidents/{_unavailable.Id}?download=true"));

        var recorded = await _h.PostFormAsync("/review", "/review?handler=PullRequest", new() { ["id"] = _unavailable.Id, ["pullRequest"] = "https://git.example.test/pr/7" });
        Assert.That(Decode(await recorded.Content.ReadAsStringAsync()), Does.Contain("https://git.example.test/pr/7"));
        Assert.That((await _h.Info.Store.GetAsync(_unavailable.Id))!.Report["outcome"]!["pull_request"]!.GetValue<string>(), Is.EqualTo("https://git.example.test/pr/7"));
    }

    [Test]
    public async Task A_duplicate_needs_the_original_and_never_appears_on_the_review_page()
    {
        var bad = await _h.PostFormAsync($"/incidents/{_unavailable.Id}", $"/incidents/{_unavailable.Id}?handler=Close", new() { ["value"] = "duplicate", ["duplicateOf"] = "INC-20990101-0001" });
        Assert.That(Decode(await bad.Content.ReadAsStringAsync()), Does.Contain("does not exist"));
        Assert.That((await _h.Info.Store.GetAsync(_unavailable.Id))!.Status, Is.EqualTo("open"));

        await _h.PostFormAsync($"/incidents/{_unavailable.Id}", $"/incidents/{_unavailable.Id}?handler=Close", new() { ["value"] = "duplicate", ["duplicateOf"] = _unknown.Id });
        Assert.That((await _h.Info.Store.GetAsync(_unavailable.Id))!.Report["outcome"]!["note"]!.GetValue<string>(), Does.Contain($"Duplicate of {_unknown.Id}"));
        Assert.That(Decode(await _h.GetAsync("/review")), Does.Not.Contain(_unavailable.Id));
    }

    [Test]
    public async Task A_closed_incident_cannot_be_closed_or_acknowledged_again()
    {
        await _h.PostFormAsync($"/incidents/{_unavailable.Id}", $"/incidents/{_unavailable.Id}?handler=Close", new() { ["value"] = "wontfix" });
        var again = await _h.PostFormAsync($"/incidents/{_unavailable.Id}", $"/incidents/{_unavailable.Id}?handler=Close", new() { ["value"] = "fixed" });
        Assert.That(Decode(await again.Content.ReadAsStringAsync()), Does.Contain("already closed"));
        var ack = await _h.PostFormAsync($"/incidents/{_unavailable.Id}", $"/incidents/{_unavailable.Id}?handler=Acknowledge", []);
        Assert.That(Decode(await ack.Content.ReadAsStringAsync()), Does.Contain("only an open incident"));
        Assert.That((await _h.Info.Store.GetAsync(_unavailable.Id))!.Outcome, Is.EqualTo("wontfix"));
    }

    [Test]
    public async Task Investigate_again_says_so_when_no_investigator_is_available_and_queues_when_one_is()
    {
        var none = await _h.PostFormAsync($"/incidents/{_unknown.Id}", $"/incidents/{_unknown.Id}?handler=Investigate", []);
        Assert.That(Decode(await none.Content.ReadAsStringAsync()), Does.Contain("No investigator is available"));
        Assert.That(_h.Investigator.Queued, Is.Empty);

        _h.Investigator.IsAvailable = true;
        var queued = await _h.PostFormAsync($"/incidents/{_unknown.Id}", $"/incidents/{_unknown.Id}?handler=Investigate", []);
        Assert.That(Decode(await queued.Content.ReadAsStringAsync()), Does.Contain("queued for investigation"));
        Assert.That(_h.Investigator.Queued.Single().IncidentId, Is.EqualTo(_unknown.Id));
        Assert.That((await _h.Info.Store.GetInvestigationAsync(_unknown.Id))!.Status, Is.EqualTo("queued"));
    }

    // ---- drafts ----

    [Test]
    public async Task The_drafts_page_lists_drafts_and_sending_one_creates_the_ticket()
    {
        var drafts = await _h.Info.Store.NotificationsAsync(kind: "draft");
        var draft = drafts.Single(d => d.IncidentId == _unknown.Id);
        var page = Decode(await _h.GetAsync("/drafts"));
        Assert.That(page, Does.Contain(_unknown.Id).And.Contain("ticket_webhook").And.Contain("Send draft"));
        var before = _h.Webhook.Requests.Count;

        var response = await _h.PostFormAsync("/drafts", "/drafts?handler=Send", new() { ["notificationId"] = draft.Id.ToString() });

        Assert.That(Decode(await response.Content.ReadAsStringAsync()), Does.Contain("Ticket created"));
        Assert.That(_h.Webhook.Requests.Count, Is.EqualTo(before + 1));
        Assert.That(_h.Webhook.Requests.Last().IdempotencyKey, Is.EqualTo($"{_unknown.Id}:created"));
        Assert.That((await _h.Info.Store.GetNotificationAsync(draft.Id))!.Kind, Is.EqualTo("sent"));
        Assert.That(Decode(await _h.GetAsync("/drafts")), Does.Not.Contain("Send draft"));
    }

    // ---- search ----

    [Test]
    public async Task Search_by_trace_id_runs_logs_by_correlation_as_the_dashboard_user_and_audits_it()
    {
        var page = Decode(await _h.GetAsync($"/search?trace={Rig.Trace}"));
        Assert.That(page, Does.Contain("logs_by_correlation").And.Contain($"dashboard:{Environment.UserName}").And.Contain("task.create.unavailable").And.Contain("handler=OpenIncident"));
        var audit = (await _h.Info.Store.ToolAuditAsync()).Where(a => a.Caller.StartsWith("dashboard:")).ToList();
        Assert.That(audit.Select(a => a.Tool), Is.EqualTo(new[] { "logs_by_correlation" }));
        Assert.That(audit[0].ArgumentsJson, Does.Contain(Rig.Trace));
    }

    [Test]
    public async Task Search_by_entity_and_id_runs_audit_trail_by_entity()
    {
        var page = Decode(await _h.GetAsync($"/search?entity=organization&id={Rig.OrgId}"));
        Assert.That(page, Does.Contain("audit_trail_by_entity").And.Contain("changes").And.Contain("IsDeleted"));
    }

    [Test]
    public async Task A_search_that_is_not_valid_is_denied_by_the_executor_and_offers_no_incident()
    {
        var page = Decode(await _h.GetAsync("/search?trace=not-a-trace-id"));
        Assert.That(page, Does.Contain("denied").And.Not.Contain("handler=OpenIncident"));
    }

    [Test]
    public async Task Open_incident_from_a_search_creates_one_manual_incident()
    {
        var response = await _h.PostFormAsync($"/search?trace={Rig.Trace}", "/search?handler=OpenIncident", new() { ["trace"] = Rig.Trace, ["note"] = "customer called" });
        var page = Decode(await response.Content.ReadAsStringAsync());
        Assert.That(page, Does.Contain("Incident opened manually.").And.Contain("manual"));
        var manual = (await _h.Info.Store.ListAsync(new IncidentFilter(Trigger: "manual"))).Single();
        Assert.That(manual.Report["correlation"]!["trace_ids"]![0]!.GetValue<string>(), Is.EqualTo(Rig.Trace));
        await _h.PostFormAsync($"/search?trace={Rig.Trace}", "/search?handler=OpenIncident", new() { ["trace"] = Rig.Trace });
        Assert.That((await _h.Info.Store.ListAsync(new IncidentFilter(Trigger: "manual"))).Count, Is.EqualTo(1), "one incident per trace while it is open");
    }

    // ---- signals ----

    [Test]
    public async Task The_signals_page_shows_counters_noise_with_expiry_and_unknown_templates_with_their_five_fields()
    {
        var page = Decode(await _h.GetAsync("/signals"));
        Assert.That(page, Does.Contain("Counters per signature").And.Contain("task.create.unavailable"));
        Assert.That(page, Does.Contain("Accepted noise").And.Contain("No signature is accepted as noise."));
        var template = ((MyApp.Monitor.Analysis.IUnknownTemplateSink)_h.Info.Store).All().Single();
        Assert.That(page, Does.Contain(template.TemplateId).And.Contain("Vendor.Widgets.Engine").And.Contain("Exception type").And.Contain("Top frame").And.Contain("Event name"));
        Assert.That(page, Does.Contain("A widget exploded"));
    }

    [Test]
    public async Task A_suppression_window_added_on_the_signals_page_is_stored_and_listed()
    {
        var start = _h.Time.GetUtcNow().AddMinutes(-1).ToString("yyyy-MM-dd HH:mm");
        var end = _h.Time.GetUtcNow().AddHours(1).ToString("yyyy-MM-dd HH:mm");
        var response = await _h.PostFormAsync("/signals", "/signals?handler=AddWindow", new() { ["name"] = "deploy", ["match"] = "task.*", ["startsAt"] = start, ["endsAt"] = end, ["reason"] = "planned" });
        Assert.That(Decode(await response.Content.ReadAsStringAsync()), Does.Contain("Suppression window added.").And.Contain("deploy"));
        Assert.That((await _h.Info.Store.SuppressionWindowsAsync()).Single().CreatedBy, Is.EqualTo(Environment.UserName));

        var bad = await _h.PostFormAsync("/signals", "/signals?handler=AddWindow", new() { ["name"] = "x", ["match"] = "*", ["startsAt"] = end, ["endsAt"] = start });
        Assert.That(Decode(await bad.Content.ReadAsStringAsync()), Does.Contain("must end after it starts"));
    }

    // ---- sources ----

    [Test]
    public async Task The_sources_page_shows_health_offsets_pack_version_and_the_investigator()
    {
        await RunHarness.WaitForAsync(async () => (await _h.Info.Store.AllOffsetsAsync()).Count > 0, "an offset");
        var page = Decode(await _h.GetAsync("/sources"));
        Assert.That(page, Does.Contain("Source health").And.Contain("applogs").And.Contain("log_files").And.Contain("Healthy"));
        Assert.That(page, Does.Contain("File offsets").And.Contain("myapp-api-20261106.jsonl"));
        Assert.That(page, Does.Contain("1.4.0+abc1234").And.Contain("Investigator").And.Contain("rules only"));
        Assert.That(page, Does.Contain("Watermarks").And.Contain("Discovery"));
    }

    // ---- JSON API ----

    [Test]
    public async Task The_json_api_lists_and_returns_reports_and_filters_by_status_and_severity()
    {
        var list = JsonNode.Parse(await _h.GetAsync("/api/incidents"))!.AsArray();
        Assert.That(list.Select(i => i!["id"]!.GetValue<string>()), Is.EquivalentTo(new[] { _unavailable.Id, _unknown.Id }));
        Assert.That(JsonNode.Parse(await _h.GetAsync("/api/incidents?severity=Error&status=open"))!.AsArray().Count, Is.EqualTo(2));
        Assert.That(JsonNode.Parse(await _h.GetAsync("/api/incidents?status=closed"))!.AsArray(), Is.Empty);
        Assert.That(JsonNode.Parse(await _h.GetAsync("/api/incidents?severity=Warning"))!.AsArray(), Is.Empty);

        var one = JsonNode.Parse(await _h.GetAsync($"/api/incidents/{_unavailable.Id}"))!;
        Assert.That(one["id"]!.GetValue<string>(), Is.EqualTo(_unavailable.Id));
        Assert.That(one["schema_version"]!.GetValue<int>(), Is.EqualTo(1));
        using var missing = await _h.Client.GetAsync("/api/incidents/INC-20990101-0001");
        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        using var download = await _h.Client.GetAsync($"/api/incidents/{_unavailable.Id}?download=true");
        Assert.That(download.Content.Headers.ContentDisposition!.FileName, Is.EqualTo($"{_unavailable.Id}.json"));
    }

    [Test]
    public async Task The_health_endpoint_answers_in_the_shape_of_the_backends_health()
    {
        var health = JsonNode.Parse(await _h.GetAsync("/api/health"))!;
        Assert.That(health["status"]!.GetValue<string>(), Is.EqualTo("Healthy"));
        Assert.That(health["checks"]!.AsArray().Select(c => c!["name"]!.GetValue<string>()), Is.EqualTo(new[] { "store", "sources" }));
    }

    [Test]
    public async Task Ingest_accepts_a_valid_report_idempotently_and_refuses_an_invalid_or_conflicting_one()
    {
        var report = (JsonObject)JsonNode.Parse(_unavailable.ReportJson)!;
        report["id"] = "INC-20261106-0099";
        report["fingerprint"] = "aaaaaaaaaaaaaaaa";
        report["environment"] = "site-a";
        report["evidence"]![0]!["excerpt"] = $"contact {RunHarness.Planted}";

        async Task<(HttpStatusCode Status, JsonNode Body)> Post(JsonNode body)
        {
            using var response = await _h.Client.PostAsync("/api/incidents/ingest", new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
            return (response.StatusCode, JsonNode.Parse(await response.Content.ReadAsStringAsync())!);
        }

        var created = await Post(report);
        Assert.That(created.Status, Is.EqualTo(HttpStatusCode.Created), created.Body.ToJsonString());
        Assert.That(created.Body["id"]!.GetValue<string>(), Is.EqualTo("INC-20261106-0099"));
        var stored = (await _h.Info.Store.GetAsync("INC-20261106-0099"))!;
        Assert.That(stored.Environment, Is.EqualTo("site-a"));
        Assert.That(stored.ReportJson, Does.Not.Contain(RunHarness.Planted).And.Contain("[email]"), "masked again on the way in");

        Assert.That((await Post(report)).Status, Is.EqualTo(HttpStatusCode.OK), "a retry from the site is idempotent");
        Assert.That((await _h.Info.Store.ListAsync(new IncidentFilter(Trigger: "severity"))).Count(i => i.Id == "INC-20261106-0099"), Is.EqualTo(1));
        Assert.That(Decode(await _h.GetAsync("/incidents/INC-20261106-0099")), Does.Contain("site-a"));

        var conflicting = (JsonObject)report.DeepClone();
        conflicting["fingerprint"] = "bbbbbbbbbbbbbbbb";
        Assert.That((await Post(conflicting)).Status, Is.EqualTo(HttpStatusCode.Conflict));

        var invalid = (JsonObject)report.DeepClone();
        invalid["severity"] = "Catastrophic";
        invalid["id"] = "INC-20261106-0098";
        var refused = await Post(invalid);
        Assert.That(refused.Status, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(refused.Body["errors"]!.AsArray().Count, Is.GreaterThan(0));
        Assert.That(await _h.Info.Store.GetAsync("INC-20261106-0098"), Is.Null);

        using var notJson = await _h.Client.PostAsync("/api/incidents/ingest", new StringContent("nope", Encoding.UTF8, "application/json"));
        Assert.That(notJson.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    // ---- who can reach it ----

    [Test]
    public async Task A_form_post_without_the_antiforgery_token_is_refused()
    {
        using var response = await _h.Client.PostAsync($"/incidents/{_unavailable.Id}?handler=Acknowledge", new FormUrlEncodedContent([]));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await _h.Info.Store.GetAsync(_unavailable.Id))!.Status, Is.EqualTo("open"));
    }

    [Test]
    public async Task In_local_mode_a_request_for_another_host_name_is_refused_and_the_server_listens_on_loopback_only()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "monitor.example.test";
        using var response = await _h.Client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(IPAddress.IsLoopback(IPAddress.Parse(_h.Info.DashboardUri!.Host)), Is.True);
    }
}
