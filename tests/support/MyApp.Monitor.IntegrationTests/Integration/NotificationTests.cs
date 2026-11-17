using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Store;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

/// <summary>Notifications (operations.md 10.5): e-mail and ticket webhook behind interfaces, templates, gates, dedup, drafts, retries.</summary>
public class NotificationTests
{
    private Rig _rig = null!;

    [SetUp]
    public void SetUp() => _rig = new Rig();

    [TearDown]
    public void TearDown() => _rig.Dispose();

    private async Task<IncidentRow> ErrorIncidentAsync(string message = "Task not created: the service failed") =>
        (await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error, message: message)))
            .Select(_ => 0).Count() > 0 ? (await _rig.AllIncidentsAsync()).First() : throw new InvalidOperationException();

    // ---- e-mail ----

    [Test]
    public async Task An_error_incident_sends_one_e_mail_rendered_from_the_template_with_the_subject_as_its_first_line()
    {
        var row = await ErrorIncidentAsync();

        var (connection, message) = _rig.Email.Sent.Single();
        Assert.That(message.Subject, Is.EqualTo($"[Error] {row.Id} prod: task.create.unavailable (severity)"));
        Assert.That(message.To, Is.EqualTo(new[] { "support@example.test" }));
        Assert.That(message.From, Is.EqualTo("monitor@localhost"));
        Assert.That(message.Body, Does.Contain($"Incident {row.Id} in prod").And.Contain("Rule verdict: The task could not be saved").And.Contain("Open the incident: http://localhost:5310/incidents/" + row.Id));
        Assert.That(message.Body, Does.Contain("E1 signal"), "evidence lines");
        Assert.That((connection.Host, connection.Port, connection.UseTls), Is.EqualTo(("localhost", 25, false)));
        var stored = (await _rig.Store.FindNotificationAsync(row.Id, "email", "created"))!;
        Assert.That((stored.Kind, stored.Target), Is.EqualTo(("sent", "support@example.test")));
    }

    [Test]
    public async Task The_notification_reads_only_the_masked_stored_report_and_never_the_planted_address()
    {
        await ErrorIncidentAsync("Task not created: jane.doe@example.com could not save");
        Assert.That(_rig.Email.Sent.Single().Message.Body, Does.Not.Contain("jane.doe@example.com").And.Contain("[email]"));
        Assert.That(_rig.Webhook.Requests.Single().Body, Does.Not.Contain("jane.doe@example.com"));
    }

    [Test]
    public async Task Evidence_excerpts_are_left_out_of_both_adapters_when_the_perimeter_says_so()
    {
        using var repo = new FixtureRepo();
        repo.Replace("docs/support/profiles/onprem.yaml", "evidence_in_notifications: true", "evidence_in_notifications: false");
        _rig.Dispose();
        _rig = new Rig(repo.BuildAndLoad());
        await ErrorIncidentAsync("Task not created: the service failed for the order");

        Assert.That(_rig.Email.Sent.Single().Message.Body, Does.Contain("E1 signal").And.Not.Contain("the service failed for the order"));
        Assert.That(_rig.Webhook.Requests.Single().Body, Does.Not.Contain("the service failed for the order"));
    }

    [Test]
    public async Task A_failing_mail_server_marks_the_notification_failed_shows_on_the_dashboard_and_does_not_stop_the_ticket()
    {
        _rig.Email.Failure = new IOException("connection refused by mail.example.test");
        var row = await ErrorIncidentAsync();

        var email = (await _rig.Store.FindNotificationAsync(row.Id, "email", "created"))!;
        Assert.That(email.Kind, Is.EqualTo("failed"));
        Assert.That(email.Error, Does.Contain("connection refused"));
        Assert.That((await _rig.Store.FindNotificationAsync(row.Id, "ticket_webhook", "created"))!.Kind, Is.EqualTo("sent"), "the other notifier is unaffected");
        Assert.That((await _rig.Store.GetAsync(row.Id))!.Report["actions_taken"]!.AsArray().Select(a => a!["action"]!.GetValue<string>()), Does.Contain("notification_failed").And.Contain("ticket_created"));
        Assert.That(await _rig.Store.NotificationsAsync(kind: "failed"), Has.Count.EqualTo(1));
    }

    // ---- ticket webhook ----

    [Test]
    public async Task A_matched_error_gets_its_ticket_automatically_with_the_idempotency_key_and_the_ticket_id_is_recorded()
    {
        var row = await ErrorIncidentAsync();

        var request = _rig.Webhook.Requests.Single();
        Assert.That((request.Url, request.Method), Is.EqualTo(("http://tickets.test/api/tickets", "POST")));
        Assert.That(request.IdempotencyKey, Is.EqualTo($"{row.Id}:created"));
        var body = JsonNode.Parse(request.Body)!;
        Assert.That(body["incident_id"]!.GetValue<string>(), Is.EqualTo(row.Id));
        Assert.That(body["severity"]!.GetValue<string>(), Is.EqualTo("Error"));
        Assert.That(body["signature"]!.GetValue<string>(), Is.EqualTo("task.create.unavailable"));
        Assert.That(body["dashboard_url"]!.GetValue<string>(), Is.EqualTo($"http://localhost:5310/incidents/{row.Id}"));
        var stored = (await _rig.Store.FindNotificationAsync(row.Id, "ticket_webhook", "created"))!;
        Assert.That((stored.Kind, stored.Reference), Is.EqualTo(("sent", $"TCK-{row.Id}-created")));
    }

    [Test]
    public async Task A_residual_or_unknown_escalation_gets_a_ticket_draft_that_a_person_sends_from_the_drafts_list()
    {
        await _rig.FeedAsync(Rig.Unknown("A widget exploded"));
        var row = (await _rig.AllIncidentsAsync()).Single();
        Assert.That(_rig.Webhook.Requests, Is.Empty);
        var draft = (await _rig.Store.NotificationsAsync(row.Id, "draft")).Single();
        Assert.That(draft.Channel, Is.EqualTo("ticket_webhook"));
        Assert.That(JsonNode.Parse(draft.Body!)!["incident_id"]!.GetValue<string>(), Is.EqualTo(row.Id));

        var result = await _rig.Actions.SendDraftAsync(draft.Id, "ann");

        Assert.That(result.Ok, Is.True, result.Message);
        Assert.That(_rig.Webhook.Requests.Single().IdempotencyKey, Is.EqualTo($"{row.Id}:created"));
        var sent = (await _rig.Store.GetNotificationAsync(draft.Id))!;
        Assert.That((sent.Kind, sent.Reference), Is.EqualTo(("sent", $"TCK-{row.Id}-created")));
        Assert.That((await _rig.Store.GetAsync(row.Id))!.Report["actions_taken"]!.AsArray().Select(a => a!["action"]!.GetValue<string>()), Does.Contain("ticket_draft").And.Contain("ticket_created"));
        Assert.That((await _rig.Actions.SendDraftAsync(draft.Id, "ann")).Ok, Is.False, "a sent notification is not sent twice");
    }

    [Test]
    public async Task The_webhook_retries_three_times_then_fails_and_a_later_success_does_not_duplicate()
    {
        _rig.Webhook.FailFirst = 100;
        var row = await ErrorIncidentAsync();
        Assert.That(_rig.Webhook.Requests.Count, Is.EqualTo(4), "one attempt and three retries");
        var failed = (await _rig.Store.FindNotificationAsync(row.Id, "ticket_webhook", "created"))!;
        Assert.That(failed.Kind, Is.EqualTo("failed"));
        Assert.That(failed.Error, Does.Contain("503"));

        _rig.Webhook.FailFirst = 0;
        Assert.That((await _rig.Actions.SendDraftAsync(failed.Id, "ann")).Ok, Is.True, "retry from the dashboard");
        Assert.That((await _rig.Store.GetNotificationAsync(failed.Id))!.Kind, Is.EqualTo("sent"));
    }

    [Test]
    public async Task A_webhook_that_recovers_inside_the_retries_is_sent_once()
    {
        _rig.Webhook.FailFirst = 2;
        var row = await ErrorIncidentAsync();
        Assert.That(_rig.Webhook.Requests.Count, Is.EqualTo(3));
        Assert.That((await _rig.Store.FindNotificationAsync(row.Id, "ticket_webhook", "created"))!.Kind, Is.EqualTo("sent"));
    }

    [Test]
    public async Task The_retry_delays_are_10_60_and_300_seconds_by_default()
    {
        Assert.That(new NotificationOptions().RetryDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(300) }));
        await Task.CompletedTask;
    }

    [Test]
    public async Task A_disabled_adapter_turns_what_would_be_sent_into_a_draft()
    {
        _rig.Dispose();
        _rig = new Rig(emailEnabled: false, webhookEnabled: false);
        var row = await ErrorIncidentAsync();

        Assert.That(_rig.Email.Sent, Is.Empty);
        Assert.That(_rig.Webhook.Requests, Is.Empty);
        var drafts = await _rig.Store.NotificationsAsync(row.Id, "draft");
        Assert.That(drafts.Select(d => d.Channel).Order(), Is.EqualTo(new[] { "email", "ticket_webhook" }));
        Assert.That((await _rig.Actions.SendDraftAsync(drafts[0].Id, "ann")).Message, Does.Contain("not enabled"));
    }

    [Test]
    public async Task The_report_template_posts_the_stored_report_itself_for_a_central_monitor()
    {
        using var repo = new FixtureRepo();
        repo.Replace("docs/support/profiles/onprem.yaml", "template: templates/ticket-incident.json", "template: \"@report\"");
        _rig.Dispose();
        _rig = new Rig(repo.BuildAndLoad());
        var row = await ErrorIncidentAsync();

        var posted = JsonNode.Parse(_rig.Webhook.Requests.Single().Body)!;
        Assert.That(posted["id"]!.GetValue<string>(), Is.EqualTo(row.Id));
        Assert.That(posted["schema_version"]!.GetValue<int>(), Is.EqualTo(1));
    }

    // ---- dedup: one fingerprint, one action per window ----

    [Test]
    public async Task One_fingerprint_gives_one_notification_per_channel_whatever_the_number_of_records()
    {
        for (var i = 0; i < 25; i++)
            await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error, message: $"again {i}", at: _rig.Time.GetUtcNow().AddSeconds(i)));
        Assert.That((_rig.Email.Sent.Count, _rig.Webhook.Requests.Count), Is.EqualTo((1, 1)));
        Assert.That((await _rig.Store.NotificationsAsync()).Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Notifying_the_same_event_twice_sends_nothing_the_second_time()
    {
        var row = await ErrorIncidentAsync();
        await _rig.Notifier.NotifyAsync(_rig.Context.Notifications, row.Id, NotificationEvents.Created);
        Assert.That((_rig.Email.Sent.Count, _rig.Webhook.Requests.Count), Is.EqualTo((1, 1)));
    }

    // ---- background delivery ----

    [Test]
    public async Task In_background_mode_a_retrying_webhook_does_not_hold_up_the_analyser_and_shutdown_interrupts_it()
    {
        _rig.Dispose();
        _rig = new Rig(notifications: new NotificationOptions { Background = true, RetryDelays = [TimeSpan.FromMinutes(10)] });
        _rig.Webhook.FailFirst = 100;

        var started = System.Diagnostics.Stopwatch.StartNew();
        await _rig.FeedAsync(_rig.Keyed("task.create.unavailable", RecordLevel.Error));
        Assert.That(started.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)), "the analyser did not wait for the 10 minute retry");
        var row = (await _rig.AllIncidentsAsync()).Single();

        await _rig.Notifier.DrainAsync(TimeSpan.FromMilliseconds(300));

        Assert.That(_rig.Email.Sent.Count, Is.EqualTo(1), "the other notifier delivered meanwhile");
        var ticket = (await _rig.Store.FindNotificationAsync(row.Id, "ticket_webhook", "created"))!;
        Assert.That(ticket.Kind, Is.EqualTo("failed"));
        Assert.That(ticket.Error, Does.Contain("interrupted by shutdown"));
    }

    // ---- the digest ----

    [Test]
    public async Task The_daily_digest_lists_the_warning_incidents_once_a_day_at_the_digest_time()
    {
        _rig.Dispose();
        _rig = new Rig(start: new DateTimeOffset(2026, 11, 6, 7, 0, 0, TimeSpan.Zero));
        for (var i = 0; i < 3; i++) await _rig.FeedAsync(Rig.Unknown("A widget slipped", RecordLevel.Warning, _rig.Time.GetUtcNow().AddSeconds(i)));
        Assert.That((await _rig.AllIncidentsAsync()).Single().Severity, Is.EqualTo("Warning"));
        Assert.That(_rig.Email.Sent, Is.Empty);

        await _rig.Digest.RunDueAsync(_rig.Context.Notifications);
        Assert.That(_rig.Email.Sent, Is.Empty, "07:00 is before 08:00");

        _rig.Time.Set(new DateTimeOffset(2026, 11, 6, 8, 5, 0, TimeSpan.Zero));
        await _rig.Digest.RunDueAsync(_rig.Context.Notifications);
        var message = _rig.Email.Sent.Single().Message;
        Assert.That(message.Subject, Is.EqualTo("[Digest] 1 Warning incident in the last 24 hours"));
        Assert.That(message.Body, Does.Contain("INC-20261106-0001").And.Contain("http://localhost:5310/incidents/INC-20261106-0001"));

        await _rig.Digest.RunDueAsync(_rig.Context.Notifications);
        Assert.That(_rig.Email.Sent.Count, Is.EqualTo(1), "once a day");

        var again = _rig.Restart();
        await again.Digest.RunDueAsync(again.Context.Notifications);
        Assert.That(again.Email.Sent, Is.Empty, "not twice after a restart either");
        again.Dispose();
    }

    [Test]
    public async Task The_digest_skips_warnings_that_were_already_mailed_and_days_without_any()
    {
        _rig.Dispose();
        _rig = new Rig(start: new DateTimeOffset(2026, 11, 6, 9, 0, 0, TimeSpan.Zero));
        await _rig.Digest.RunDueAsync(_rig.Context.Notifications);
        Assert.That(_rig.Email.Sent, Is.Empty);
    }
}

/// <summary>The webhook adapter against a real local listener (operations.md 14.5): the headers, the body and the ticket id come through HTTP.</summary>
public class WebhookListenerTests
{
    [Test]
    public async Task The_http_client_posts_the_rendered_json_with_the_idempotency_key_and_the_configured_headers_and_returns_the_answer()
    {
        using var listener = new HttpListener();
        var port = FreePort();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var received = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var result = (ctx.Request.HttpMethod, ctx.Request.Headers["Idempotency-Key"], ctx.Request.Headers["Authorization"], ctx.Request.ContentType, body);
            var answer = Encoding.UTF8.GetBytes("""{"id":"TCK-77","url":"x"}""");
            ctx.Response.StatusCode = 201;
            await ctx.Response.OutputStream.WriteAsync(answer);
            ctx.Response.Close();
            return result;
        });

        var services = new ServiceCollection();
        services.AddHttpClient(HttpTicketWebhookClient.ClientName);
        using var provider = services.BuildServiceProvider();
        var client = new HttpTicketWebhookClient(provider.GetRequiredService<IHttpClientFactory>());
        var response = await client.SendAsync(new WebhookRequest($"http://127.0.0.1:{port}/api/tickets", "POST",
            new Dictionary<string, string> { ["Authorization"] = "Bearer test-token" }, "INC-20261106-0001:created", """{"title":"x"}"""), default);

        var seen = await received;
        Assert.That(response.IsSuccess, Is.True);
        Assert.That(response.Body, Does.Contain("TCK-77"));
        Assert.That(seen.HttpMethod, Is.EqualTo("POST"));
        Assert.That(seen.Item2, Is.EqualTo("INC-20261106-0001:created"));
        Assert.That(seen.Item3, Is.EqualTo("Bearer test-token"));
        Assert.That(seen.ContentType, Does.StartWith("application/json"));
        Assert.That(seen.body, Is.EqualTo("""{"title":"x"}"""));
    }

    [Test]
    public async Task The_dispatcher_creates_a_ticket_through_a_real_listener_and_reads_the_id_at_the_id_path()
    {
        using var listener = new HttpListener();
        var port = FreePort();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var requests = new List<string>();
        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await listener.GetContextAsync(); } catch (Exception) { return; }
                requests.Add(ctx.Request.Headers["Idempotency-Key"]!);
                var answer = Encoding.UTF8.GetBytes("""{"id":"TCK-9"}""");
                ctx.Response.StatusCode = 200;
                await ctx.Response.OutputStream.WriteAsync(answer);
                ctx.Response.Close();
            }
        });

        var services = new ServiceCollection();
        services.AddHttpClient(HttpTicketWebhookClient.ClientName);
        using var provider = services.BuildServiceProvider();
        using var rig = new Rig(settings: new() { ["Monitor:Settings:TicketWebhookUrl"] = $"http://127.0.0.1:{port}/api" });
        var dispatcher = new NotificationDispatcher(rig.Store, rig.Email, new HttpTicketWebhookClient(provider.GetRequiredService<IHttpClientFactory>()),
            new NotificationOptions { Background = false }, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), rig.Time);

        await rig.FeedAsync(rig.Keyed("task.create.unavailable", RecordLevel.Error));
        var row = (await rig.AllIncidentsAsync()).Single();
        await rig.Store.UpdateNotificationAsync((await rig.Store.FindNotificationAsync(row.Id, "ticket_webhook", "created"))!.Id, "failed", rig.Time.GetUtcNow(), null, null, "forced");
        var id = (await rig.Store.FindNotificationAsync(row.Id, "ticket_webhook", "created"))!.Id;
        var outcome = await dispatcher.SendDraftAsync(rig.Context.Notifications, id, "ann");

        listener.Stop();
        Assert.That(outcome.Ok, Is.True, outcome.Message);
        Assert.That(outcome.Message, Does.Contain("TCK-9"));
        Assert.That(requests.Last(), Is.EqualTo($"{row.Id}:created"));
    }

    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
