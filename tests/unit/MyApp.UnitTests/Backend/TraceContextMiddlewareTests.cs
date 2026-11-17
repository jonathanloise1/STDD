extern alias WebApi;

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using WebApi::MyApp.WebApi.Middlewares;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

[TestFixture]
public class TraceContextMiddlewareTests
{
    [SetUp]
    public void SetUp() => Activity.Current = null;

    private static async Task<(string? TraceId, string? ParentSpanId, bool Recorded)> Run(string? traceparent)
    {
        var context = new DefaultHttpContext();
        if (traceparent is not null)
            context.Request.Headers["traceparent"] = traceparent;

        string? traceId = null;
        string? parentSpan = null;
        var recorded = false;
        await new TraceContextMiddleware(_ =>
        {
            recorded = Activity.Current is not null;
            traceId = Activity.Current?.TraceId.ToHexString();
            parentSpan = Activity.Current?.ParentSpanId.ToHexString();
            return Task.CompletedTask;
        }).InvokeAsync(context);
        return (traceId, parentSpan, recorded);
    }

    [Test]
    public async Task IncomingTraceparent_IsContinued()
    {
        var (traceId, parentSpan, _) = await Run("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        Assert.That(traceId, Is.EqualTo("4bf92f3577b34da6a3ce929d0e0e4736"));
        Assert.That(parentSpan, Is.EqualTo("00f067aa0ba902b7"));
        Assert.That(Activity.Current, Is.Null, "the activity ends with the request");
    }

    [Test]
    public async Task WithoutTraceparent_ANewTraceIdIsCreated()
    {
        var (traceId, _, recorded) = await Run(null);

        Assert.That(recorded, Is.True);
        Assert.That(traceId, Does.Match("^[0-9a-f]{32}$"));
    }

    [Test]
    public async Task MalformedTraceparent_IsIgnoredAndANewTraceIdIsCreated()
    {
        var (traceId, _, _) = await Run("garbage");

        Assert.That(traceId, Does.Match("^[0-9a-f]{32}$").And.Not.EqualTo(new string('0', 32)));
    }

    [Test]
    public async Task AnExistingActivity_IsLeftUntouched()
    {
        var existing = new Activity("host");
        existing.SetIdFormat(ActivityIdFormat.W3C);
        existing.Start();
        try
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["traceparent"] = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
            Activity? seen = null;
            await new TraceContextMiddleware(_ => { seen = Activity.Current; return Task.CompletedTask; }).InvokeAsync(context);
            Assert.That(seen, Is.SameAs(existing));
        }
        finally
        {
            existing.Stop();
        }
    }

    [Test]
    public async Task ErrorResponseAndLogEntry_ShareTheTraceIdOfTheRequest()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        var log = new CapturingLogger<ExceptionHandlingMiddleware>();
        var pipeline = new TraceContextMiddleware(
            new ExceptionHandlingMiddleware(_ => throw new InvalidOperationException(), log, new TestHostEnvironment("Production")).InvokeAsync);

        await pipeline.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var traceId = System.Text.Json.JsonDocument.Parse(context.Response.Body).RootElement.GetProperty("traceId").GetString();
        Assert.That(traceId, Is.EqualTo("0af7651916cd43dd8448eb211c80319c"));
    }
}
