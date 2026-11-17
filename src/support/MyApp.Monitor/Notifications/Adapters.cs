using System.Net.Mail;
using System.Text;

namespace MyApp.Monitor.Notifications;

public sealed record SmtpConnection(string Host, int Port, bool UseTls, string? Username, string? Password);

public sealed record EmailMessage(string From, IReadOnlyList<string> To, string Subject, string Body);

/// <summary>The e-mail adapter's transport (operations.md 10.5). A fake implements it in tests: no test sends a real message.</summary>
public interface IEmailSender
{
    Task SendAsync(SmtpConnection connection, EmailMessage message, CancellationToken ct);
}

/// <summary>SMTP with the .NET client.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    public async Task SendAsync(SmtpConnection connection, EmailMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient(connection.Host, connection.Port) { EnableSsl = connection.UseTls, Timeout = 30_000 };
        if (!string.IsNullOrEmpty(connection.Username))
            client.Credentials = new System.Net.NetworkCredential(connection.Username, connection.Password);
        using var mail = new MailMessage { From = new MailAddress(message.From), Subject = message.Subject, Body = message.Body, BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8 };
        foreach (var to in message.To) mail.To.Add(to);
        await client.SendMailAsync(mail, ct).ConfigureAwait(false);
    }
}

public sealed record WebhookRequest(string Url, string Method, IReadOnlyDictionary<string, string> Headers, string IdempotencyKey, string Body);

public sealed record WebhookResponse(int StatusCode, string Body)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}

/// <summary>The ticket webhook adapter's transport (operations.md 10.5). Tests run it against a fake or a local listener.</summary>
public interface ITicketWebhookClient
{
    Task<WebhookResponse> SendAsync(WebhookRequest request, CancellationToken ct);
}

public sealed class HttpTicketWebhookClient(IHttpClientFactory http) : ITicketWebhookClient
{
    public const string ClientName = "monitor-webhook";

    public async Task<WebhookResponse> SendAsync(WebhookRequest request, CancellationToken ct)
    {
        var client = http.CreateClient(ClientName);
        using var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Url)
        {
            Content = new StringContent(request.Body, Encoding.UTF8, "application/json"),
        };
        message.Headers.TryAddWithoutValidation("Idempotency-Key", request.IdempotencyKey);
        foreach (var (name, value) in request.Headers) message.Headers.TryAddWithoutValidation(name, value);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await client.SendAsync(message, timeout.Token).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        return new WebhookResponse((int)response.StatusCode, body);
    }
}
