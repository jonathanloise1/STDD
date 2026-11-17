using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyApp.Monitor.Store;

namespace MyApp.Monitor.Notifications;

/// <summary>
/// The daily digest of <c>Warning</c> incidents at <c>gates.email.digest_time</c> (operations.md 10.3), local time. One e-mail a day;
/// the day is recorded as a notification (<c>digest:{date}</c>) so a restart never sends it twice. Evidence excerpts never go into
/// the digest: it lists incidents and links to the dashboard.
/// </summary>
public sealed class DigestService(IIncidentStore store, IEmailSender email, IConfiguration configuration, TimeProvider time, ILogger<DigestService>? logger = null)
{
    private DateOnly _checked = DateOnly.MinValue;

    public async Task RunDueAsync(NotificationContext context, CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var local = TimeZoneInfo.ConvertTime(now, time.LocalTimeZone);
        var today = DateOnly.FromDateTime(local.Date);
        if (_checked == today) return;
        if (!TimeOnly.TryParse(context.Profile.Gates.EmailDigestTime, CultureInfo.InvariantCulture, out var at)) return;
        if (TimeOnly.FromDateTime(local.DateTime) < at) return;
        _checked = today;

        var settings = context.Settings.Email;
        if (!settings.Enabled || settings.To.Count == 0) return;
        var key = "digest:" + today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (await store.FindNotificationAsync(key, Channels.Email, NotificationEvents.Digest, ct).ConfigureAwait(false) is not null) return;

        var since = now - TimeSpan.FromHours(24);
        var warnings = (await store.ListAsync(new IncidentFilter(Severity: "Warning"), 200, ct).ConfigureAwait(false))
            .Where(i => i.Status != "closed" && i.CreatedAt >= since)
            .ToList();
        var due = new List<IncidentRow>();
        foreach (var w in warnings)
            if (await store.FindNotificationAsync(w.Id, Channels.Email, NotificationEvents.Created, ct).ConfigureAwait(false) is not { Kind: NotificationKinds.Sent })
                due.Add(w);
        if (due.Count == 0) return;

        var body = new StringBuilder();
        body.Append("[Digest] ").Append(due.Count).AppendLine(due.Count == 1 ? " Warning incident in the last 24 hours" : " Warning incidents in the last 24 hours");
        foreach (var i in due)
        {
            var report = i.Report;
            body.AppendLine()
                .AppendLine($"{i.Id} {i.Environment}: {MonitorJson.ScalarText(report["signature"]?["id"]) ?? MonitorJson.ScalarText(report["signature"]?["template_id"]) ?? "unknown"} ({i.Trigger})")
                .AppendLine($"Seen {i.Count} times from {SqliteDatabase.Iso(i.FirstSeen)} to {SqliteDatabase.Iso(i.LastSeen)}; status {i.Status}")
                .AppendLine(context.IncidentUrl(i.Id));
        }
        var text = body.ToString();
        var newline = text.IndexOf('\n');
        try
        {
            var connection = new SmtpConnection(settings.SmtpHost, settings.SmtpPort, settings.UseTls,
                settings.UsernameSetting is null ? null : configuration[settings.UsernameSetting],
                settings.PasswordSetting is null ? null : configuration[settings.PasswordSetting]);
            await email.SendAsync(connection, new EmailMessage(settings.From, settings.To, text[..newline].Trim(), text[(newline + 1)..].TrimStart('\r', '\n')), ct).ConfigureAwait(false);
            await store.AddNotificationAsync(new NotificationRow(0, key, Channels.Email, NotificationKinds.Sent, now, string.Join(", ", settings.To), $"{due.Count} incidents", null, text, NotificationEvents.Digest), ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var error = context.Masker.MaskFreeText(ex.Message, 300);
            await store.AddNotificationAsync(new NotificationRow(0, key, Channels.Email, NotificationKinds.Failed, now, null, null, error, text, NotificationEvents.Digest), ct).ConfigureAwait(false);
            logger?.LogError("The daily digest failed: {Error}", error);
        }
    }
}
