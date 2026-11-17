using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Masking;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Store;

namespace MyApp.Monitor.Notifications;

/// <summary>Delivery behaviour. The webhook retries three times, after 10, 60 and 300 seconds (operations.md 10.5).</summary>
public sealed class NotificationOptions
{
    public IReadOnlyList<TimeSpan> RetryDelays { get; set; } = [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(300)];

    /// <summary>Deliver in the background so that a retrying webhook never holds up the analyser. Tests set it to false.</summary>
    public bool Background { get; set; } = true;
}

/// <summary>The address the dashboard answers on, as links in notifications. Set from the profile and corrected once the server is listening.</summary>
public sealed class DashboardAddress
{
    private volatile TaskCompletionSource _ready = Completed();

    public string BaseUrl { get; set; } = "http://localhost:5310";

    /// <summary>Completes once the address is final. The sources wait for it so that no link in a notification is written before the server listens.</summary>
    public Task ReadyAsync => _ready.Task;

    public void Hold() => _ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Release() => _ready.TrySetResult();

    private static TaskCompletionSource Completed()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetResult();
        return tcs;
    }

    /// <summary>The first URL of a <c>dashboard.urls</c> value (semicolon separated), with a wildcard host replaced by localhost.</summary>
    public static string FromUrls(string? urls)
    {
        var first = (urls ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (string.IsNullOrEmpty(first)) return "http://localhost:5310";
        return first.Replace("://+", "://localhost").Replace("://*", "://localhost").Replace("://0.0.0.0", "://localhost").Replace("://[::]", "://localhost").TrimEnd('/');
    }
}

/// <summary>What the dispatcher needs from the loaded pack and profile: gates, adapter settings, templates and the dashboard address.</summary>
public sealed record NotificationContext(
    Profile Profile, IReadOnlyDictionary<string, string> Templates, DashboardAddress Address, Gates Gates, NotificationSettings Settings, Masker Masker)
{
    public static NotificationContext Create(Profile profile, OperationsPack pack, DashboardAddress address, Masker masker) =>
        new(profile, pack.Templates, address, new Gates(profile.Gates), NotificationSettings.From(profile.Notifications), masker);

    /// <summary>A template of the pack by the path the profile gives (<c>templates/email-incident.txt</c>); the pack keys them by file name.</summary>
    public string? Template(string path) =>
        Templates.TryGetValue(path, out var text) ? text
        : path.StartsWith("templates/", StringComparison.Ordinal) && Templates.TryGetValue(path["templates/".Length..], out text) ? text
        : null;

    public string DashboardBaseUrl => Address.BaseUrl.TrimEnd('/');

    public string IncidentUrl(string incidentId) => $"{DashboardBaseUrl}/incidents/{incidentId}";
}

public sealed record SendOutcome(bool Ok, string Message);

/// <summary>
/// Gates, templates and adapters (operations.md 10.3, 10.5). An incident event gives at most one notification per channel
/// (the idempotency key is the incident id and the event); a failing adapter marks its notification <c>failed</c> and never stops
/// the others. Notifications read only the stored, already masked report.
/// </summary>
public sealed class NotificationDispatcher(
    IIncidentStore store, IEmailSender email, ITicketWebhookClient webhook, NotificationOptions options, IConfiguration configuration,
    TimeProvider time, ILogger<NotificationDispatcher>? logger = null)
{
    private readonly ConcurrentDictionary<Task, byte> _running = new();
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>Plans and delivers the notifications of one incident event. In background mode it returns once the work is queued.</summary>
    public async Task NotifyAsync(NotificationContext context, string incidentId, string eventName, CancellationToken ct = default)
    {
        var incident = await store.GetAsync(incidentId, ct).ConfigureAwait(false);
        if (incident is null) return;
        var report = incident.Report;
        var facts = new IncidentFacts(
            incident.Severity, incident.Trigger, MonitorJson.ScalarText(report["signature"]?["kind"]) ?? "none",
            report["rule_verdict"] is JsonObject, report["model_verdict"] is JsonObject);

        foreach (var planned in context.Gates.Plan(eventName, facts))
        {
            if (!options.Background) { await DeliverAsync(context, incidentId, planned, CancellationToken.None).ConfigureAwait(false); continue; }
            var task = Task.Run(() => DeliverAsync(context, incidentId, planned, _stopping.Token));
            _running[task] = 0;
            _ = task.ContinueWith(t => _running.TryRemove(t, out _), TaskScheduler.Default);
        }
    }

    /// <summary>Waits for the deliveries in flight (shutdown and tests). Deliveries still retrying are interrupted after <paramref name="grace"/>.</summary>
    public async Task DrainAsync(TimeSpan grace)
    {
        var all = Task.WhenAll(_running.Keys.ToArray());
        if (await Task.WhenAny(all, Task.Delay(grace)).ConfigureAwait(false) != all)
        {
            _stopping.Cancel();
            await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
        }
    }

    private async Task DeliverAsync(NotificationContext context, string incidentId, PlannedNotification planned, CancellationToken ct)
    {
        try
        {
            if (await store.FindNotificationAsync(incidentId, planned.Channel, planned.Event, CancellationToken.None).ConfigureAwait(false) is not null) return;   // dedup
            var incident = await store.GetAsync(incidentId, CancellationToken.None).ConfigureAwait(false);
            if (incident is null) return;

            var enabled = planned.Channel == Channels.Email ? context.Settings.Email.Enabled && context.Settings.Email.To.Count > 0 : context.Settings.Webhook.Enabled;
            var rendered = Render(context, planned.Channel, incident);
            if (rendered.Error is not null)
            {
                await RecordFailureAsync(incident, planned, null, rendered.Error).ConfigureAwait(false);
                return;
            }

            if (planned.Draft || !enabled)
            {
                var id = await store.AddNotificationAsync(new NotificationRow(
                    0, incidentId, planned.Channel, NotificationKinds.Draft, time.GetUtcNow(), null, null, enabled ? null : "adapter not enabled in the profile", rendered.Body, planned.Event)).ConfigureAwait(false);
                await RecordAsync(incidentId, planned.Channel == Channels.Email ? ReportActions.EmailDraft : ReportActions.TicketDraft, $"notification {id}", "notification",
                    new JsonObject { ["channel"] = planned.Channel, ["kind"] = NotificationKinds.Draft, ["event"] = planned.Event, ["notification"] = id }).ConfigureAwait(false);
                return;
            }

            var outcome = await SendAsync(context, planned.Channel, incident, rendered.Body!, planned.Event, ct).ConfigureAwait(false);
            if (outcome.Error is null)
            {
                var id = await store.AddNotificationAsync(new NotificationRow(
                    0, incidentId, planned.Channel, NotificationKinds.Sent, time.GetUtcNow(), outcome.Target, outcome.Reference, null, rendered.Body, planned.Event)).ConfigureAwait(false);
                await RecordAsync(incidentId, planned.Channel == Channels.Email ? ReportActions.EmailSent : ReportActions.TicketCreated, outcome.Reference ?? $"notification {id}", "notification",
                    new JsonObject { ["channel"] = planned.Channel, ["kind"] = NotificationKinds.Sent, ["event"] = planned.Event, ["notification"] = id, ["reference"] = outcome.Reference }).ConfigureAwait(false);
            }
            else await RecordFailureAsync(incident, planned, rendered.Body, outcome.Error).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Notification {Channel} for {Incident} failed unexpectedly", planned.Channel, incidentId);
        }
    }

    /// <summary>Sends a draft a dashboard user approved (or retries a failed notification).</summary>
    public async Task<SendOutcome> SendDraftAsync(NotificationContext context, long notificationId, string user, CancellationToken ct = default)
    {
        var row = await store.GetNotificationAsync(notificationId, ct).ConfigureAwait(false);
        if (row is null) return new SendOutcome(false, "No such notification.");
        if (row.Kind is not (NotificationKinds.Draft or NotificationKinds.Failed)) return new SendOutcome(false, "Only a draft or a failed notification can be sent.");
        var incident = await store.GetAsync(row.IncidentId, ct).ConfigureAwait(false);
        if (incident is null) return new SendOutcome(false, "The incident no longer exists.");

        var enabled = row.Channel == Channels.Email ? context.Settings.Email.Enabled && context.Settings.Email.To.Count > 0 : context.Settings.Webhook.Enabled;
        if (!enabled) return new SendOutcome(false, $"The {row.Channel} adapter is not enabled in the profile.");

        var rendered = Render(context, row.Channel, incident);   // rendered again: the report may have changed since the draft
        if (rendered.Error is not null) return new SendOutcome(false, rendered.Error);
        var outcome = await SendAsync(context, row.Channel, incident, rendered.Body!, row.Event, ct).ConfigureAwait(false);
        if (outcome.Error is not null)
        {
            await store.UpdateNotificationAsync(row.Id, NotificationKinds.Failed, time.GetUtcNow(), null, null, outcome.Error, CancellationToken.None).ConfigureAwait(false);
            await RecordAsync(row.IncidentId, ReportActions.NotificationFailed, $"{row.Channel}: {outcome.Error}", "notification",
                new JsonObject { ["channel"] = row.Channel, ["kind"] = NotificationKinds.Failed, ["notification"] = row.Id, ["error"] = outcome.Error }).ConfigureAwait(false);
            logger?.LogError("Notification {Channel} for {Incident} failed: {Error}", row.Channel, row.IncidentId, outcome.Error);
            return new SendOutcome(false, outcome.Error);
        }
        await store.UpdateNotificationAsync(row.Id, NotificationKinds.Sent, time.GetUtcNow(), outcome.Target, outcome.Reference, null, CancellationToken.None).ConfigureAwait(false);
        await RecordAsync(row.IncidentId, row.Channel == Channels.Email ? ReportActions.EmailSent : ReportActions.TicketCreated, outcome.Reference ?? $"notification {row.Id}", "notification",
            new JsonObject { ["channel"] = row.Channel, ["kind"] = NotificationKinds.Sent, ["notification"] = row.Id, ["approved_by"] = user, ["reference"] = outcome.Reference }).ConfigureAwait(false);
        return new SendOutcome(true, row.Channel == Channels.Email ? "E-mail sent." : $"Ticket created{(outcome.Reference is null ? "" : ": " + outcome.Reference)}.");
    }

    // ---- rendering ----

    private sealed record Rendered(string? Body, string? Error);

    private static Rendered Render(NotificationContext context, string channel, IncidentRow incident)
    {
        var report = incident.Report;
        var url = context.IncidentUrl(incident.Id);
        var excerpts = context.Profile.EvidenceInNotifications;
        if (channel == Channels.TicketWebhook && context.Settings.Webhook.Template == NotificationSettings.ReportTemplate)
        {
            if (!excerpts && report["evidence"] is JsonArray evidence)
                foreach (var item in evidence.OfType<JsonObject>()) item["excerpt"] = "";
            return new Rendered(MonitorJson.ToCompact(report), null);
        }

        var path = channel == Channels.Email ? context.Settings.Email.Template : context.Settings.Webhook.Template;
        if (context.Template(path) is not { } template) return new Rendered(null, $"template '{path}' is not in the pack");
        var text = TemplateRenderer.Render(template, report, url, excerpts).Text;
        if (channel == Channels.TicketWebhook)
        {
            try { JsonNode.Parse(text); }
            catch (JsonException ex) { return new Rendered(null, "the ticket template did not render valid JSON: " + ex.Message); }
        }
        return new Rendered(text, null);
    }

    // ---- delivery ----

    private sealed record Delivery(string? Target, string? Reference, string? Error);

    private async Task<Delivery> SendAsync(NotificationContext context, string channel, IncidentRow incident, string body, string eventName, CancellationToken ct)
    {
        if (channel == Channels.Email) return await SendEmailAsync(context.Settings.Email, body, context.Masker, ct).ConfigureAwait(false);
        return await SendWebhookAsync(context.Settings.Webhook, incident.Id, eventName, body, context.Masker, ct).ConfigureAwait(false);
    }

    private async Task<Delivery> SendEmailAsync(EmailSettings settings, string body, Masker masker, CancellationToken ct)
    {
        var newline = body.IndexOf('\n');
        var subject = (newline < 0 ? body : body[..newline]).Trim();
        var text = newline < 0 ? "" : body[(newline + 1)..].TrimStart('\r', '\n');
        var target = string.Join(", ", settings.To);
        try
        {
            var connection = new SmtpConnection(
                settings.SmtpHost, settings.SmtpPort, settings.UseTls,
                settings.UsernameSetting is null ? null : configuration[settings.UsernameSetting],
                settings.PasswordSetting is null ? null : configuration[settings.PasswordSetting]);
            await email.SendAsync(connection, new EmailMessage(settings.From, settings.To, subject, text), ct).ConfigureAwait(false);
            return new Delivery(target, null, null);
        }
        catch (OperationCanceledException) { return new Delivery(target, null, "interrupted by shutdown"); }
        catch (Exception ex) { return new Delivery(target, null, Short(masker, ex.Message)); }
    }

    private async Task<Delivery> SendWebhookAsync(WebhookSettings settings, string incidentId, string eventName, string body, Masker masker, CancellationToken ct)
    {
        var request = new WebhookRequest(settings.Url, settings.Method, settings.Headers, $"{incidentId}:{eventName}", body);
        string? error = null;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await webhook.SendAsync(request, ct).ConfigureAwait(false);
                if (response.IsSuccess) return new Delivery(settings.Url, TicketId(response.Body, settings.IdPath), null);
                error = $"the ticket system answered HTTP {response.StatusCode}";
            }
            catch (OperationCanceledException) { return new Delivery(settings.Url, null, "interrupted by shutdown"); }
            catch (Exception ex) { error = Short(masker, ex.Message); }

            if (attempt >= options.RetryDelays.Count) return new Delivery(settings.Url, null, error);
            try { if (options.RetryDelays[attempt] > TimeSpan.Zero) await Task.Delay(options.RetryDelays[attempt], ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return new Delivery(settings.Url, null, error + " (interrupted by shutdown)"); }
        }
    }

    private static string? TicketId(string responseBody, string idPath)
    {
        try { return MonitorJson.ScalarText(MonitorJson.Get(MonitorJson.Parse(responseBody), idPath)); }
        catch (JsonException) { return null; }
    }

    // ---- recording ----

    private async Task RecordFailureAsync(IncidentRow incident, PlannedNotification planned, string? body, string error)
    {
        var id = await store.AddNotificationAsync(new NotificationRow(
            0, incident.Id, planned.Channel, NotificationKinds.Failed, time.GetUtcNow(), null, null, error, body, planned.Event)).ConfigureAwait(false);
        await RecordAsync(incident.Id, ReportActions.NotificationFailed, $"{planned.Channel}: {error}", "notification",
            new JsonObject { ["channel"] = planned.Channel, ["kind"] = NotificationKinds.Failed, ["event"] = planned.Event, ["notification"] = id, ["error"] = error }).ConfigureAwait(false);
        logger?.LogError("Notification {Channel} for {Incident} failed: {Error}", planned.Channel, incident.Id, error);
    }

    private Task RecordAsync(string incidentId, string action, string reference, string eventKind, JsonObject data) =>
        store.UpdateAsync(incidentId, time.GetUtcNow(), report =>
        {
            IncidentReports.AddAction(report, time.GetUtcNow(), action, reference);
            return IncidentChange.Of(eventKind, data);
        });

    private static string Short(Masker masker, string message) => masker.MaskFreeText(message, 300);
}
