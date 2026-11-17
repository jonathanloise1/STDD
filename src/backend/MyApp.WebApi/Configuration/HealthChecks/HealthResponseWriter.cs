using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MyApp.WebApi.Configuration.HealthChecks;

/// <summary>
/// The body of <c>/health</c> (operations.md 6.4): status and checks, never exception text.
/// </summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = (long)e.Value.Duration.TotalMilliseconds
            })
        };

        context.Response.ContentType = "application/json";
        return context.Response.Body.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions)).AsTask();
    }
}
