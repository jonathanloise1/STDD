using System.Globalization;
using System.Text.Json.Nodes;

namespace MyApp.Monitor.Notifications;

public sealed record EmailSettings(
    bool Enabled, string SmtpHost, int SmtpPort, bool UseTls, string From, IReadOnlyList<string> To, string? UsernameSetting, string? PasswordSetting, string Template);

public sealed record WebhookSettings(
    bool Enabled, string Url, string Method, IReadOnlyDictionary<string, string> Headers, string Template, string IdPath);

/// <summary>The <c>notifications</c> section of the profile (operations.md 10.5), with placeholders already resolved.</summary>
public sealed record NotificationSettings(EmailSettings Email, WebhookSettings Webhook)
{
    /// <summary>The template value that makes the webhook post the stored report itself (the hybrid setup of 12.4).</summary>
    public const string ReportTemplate = "@report";

    public static NotificationSettings From(JsonNode? notifications)
    {
        static string Str(JsonNode? n, string fallback = "") => MonitorJson.ScalarText(n) is { Length: > 0 } s ? s : fallback;
        static bool Bool(JsonNode? n) => bool.TryParse(MonitorJson.ScalarText(n), out var b) && b;
        static int Int(JsonNode? n, int fallback) => int.TryParse(MonitorJson.ScalarText(n), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

        var email = notifications?["email"] as JsonObject;
        var hook = notifications?["ticket_webhook"] as JsonObject;
        var to = (email?["to"] as JsonArray)?.Select(x => MonitorJson.ScalarText(x) ?? "").Where(s => s.Length > 0).ToList() ?? [];
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in (hook?["headers"] as JsonObject) ?? [])
            if (MonitorJson.ScalarText(kv.Value) is { Length: > 0 } value) headers[kv.Key] = value;   // an empty value means "not configured"

        return new NotificationSettings(
            new EmailSettings(Bool(email?["enabled"]), Str(email?["smtp_host"], "localhost"), Int(email?["smtp_port"], 25), Bool(email?["use_tls"]),
                Str(email?["from"], "monitor@localhost"), to, MonitorJson.ScalarText(email?["username_setting"]), MonitorJson.ScalarText(email?["password_setting"]),
                Str(email?["template"], "templates/email-incident.txt")),
            new WebhookSettings(Bool(hook?["enabled"]) && Str(hook?["url"]).Length > 0, Str(hook?["url"]), Str(hook?["method"], "POST").ToUpperInvariant(), headers,
                Str(hook?["template"], "templates/ticket-incident.json"), Str(hook?["id_path"], "id")));
    }
}
