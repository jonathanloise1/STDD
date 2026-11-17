extern alias WebApi;

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WebApi::MyApp.WebApi.Configuration.HealthChecks;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

[TestFixture]
public class HealthResponseWriterTests
{
    [Test]
    public async Task Body_HasStatusAndChecks_AndNeverExceptionText()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["sqlserver"] = new(HealthStatus.Unhealthy, "SQL Server is unreachable.",
                TimeSpan.FromMilliseconds(12), new InvalidOperationException("Password=hunter2"), null)
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(15));
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await HealthResponseWriter.WriteAsync(context, report);

        context.Response.Body.Position = 0;
        var text = new StreamReader(context.Response.Body).ReadToEnd();
        Assert.That(text, Does.Not.Contain("hunter2").And.Not.Contain("unreachable"));
        var body = JsonDocument.Parse(text).RootElement;
        Assert.That(body.GetProperty("status").GetString(), Is.EqualTo("Unhealthy"));
        var check = body.GetProperty("checks")[0];
        Assert.That(check.GetProperty("name").GetString(), Is.EqualTo("sqlserver"));
        Assert.That(check.GetProperty("status").GetString(), Is.EqualTo("Unhealthy"));
        Assert.That(check.GetProperty("durationMs").GetInt64(), Is.EqualTo(12));
        Assert.That(context.Response.ContentType, Is.EqualTo("application/json"));
    }
}
