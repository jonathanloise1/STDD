extern alias WebApi;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using MyApp.Application.Exceptions;
using MyApp.Application.Logging;
using WebApi::MyApp.WebApi.Middlewares;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

[TestFixture]
public class ExceptionHandlingMiddlewareTests
{
    private const string TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
    private const string Route = "api/organizations/{organizationId}/tasks";

    private Activity _activity = null!;

    [SetUp]
    public void SetUp()
    {
        _activity = new Activity("test");
        _activity.SetIdFormat(ActivityIdFormat.W3C);
        _activity.SetParentId(TraceParent);
        _activity.Start();
    }

    [TearDown]
    public void TearDown() => _activity.Stop();

    private sealed record Result(HttpContext Context, JsonElement? Body, CapturingLogger<ExceptionHandlingMiddleware> Log)
    {
        public JsonElement B => Body!.Value;
        public IEnumerable<string> Fields => B.EnumerateObject().Select(p => p.Name);
    }

    private static async Task<Result> Run(Exception exception, string environment = "Production", CancellationToken aborted = default)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestAborted = aborted;
        context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask, RoutePatternFactory.Parse(Route), 0,
            EndpointMetadataCollection.Empty, "test"));
        var log = new CapturingLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(_ => throw exception, log, new TestHostEnvironment(environment));

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        JsonElement? body = context.Response.Body.Length == 0
            ? null
            : JsonDocument.Parse(context.Response.Body).RootElement.Clone();
        return new Result(context, body, log);
    }

    [Test]
    public async Task OutcomeException_IsReturnedWithItsStatusAndTheKeyAsCode_AndWritesNoEntry()
    {
        var r = await Run(new OutcomeException(TasksLog.Keys.TaskCreateNotAllowed, 403, "Your role does not allow this change."));

        Assert.That(r.Context.Response.StatusCode, Is.EqualTo(403));
        Assert.That(r.Context.Response.ContentType, Is.EqualTo("application/problem+json"));
        Assert.That(r.Fields, Is.EqualTo(new[] { "type", "title", "status", "detail", "code", "traceId" }));
        Assert.That(r.B.GetProperty("type").GetString(), Is.EqualTo("about:blank"));
        Assert.That(r.B.GetProperty("title").GetString(), Is.EqualTo("Forbidden"));
        Assert.That(r.B.GetProperty("status").GetInt32(), Is.EqualTo(403));
        Assert.That(r.B.GetProperty("detail").GetString(), Is.EqualTo("Your role does not allow this change."));
        Assert.That(r.B.GetProperty("code").GetString(), Is.EqualTo(TasksLog.Keys.TaskCreateNotAllowed));
        Assert.That(r.Log.Entries, Is.Empty);
    }

    [Test]
    public async Task TypedOutcomeExceptions_MapToTheirStatus()
    {
        var key = TasksLog.Keys.TaskCreateNotAllowed;
        var cases = new (OutcomeException Exception, int Status)[]
        {
            (new AccessLostException(key, "message"), 403),
            (new ResourceNotFoundException(key, "message"), 404),
            (new ConflictException(key, "message"), 409),
            (new DependencyUnavailableException(key, "message"), 503),
            (new RequestValidationException(key, "message"), 400)
        };

        foreach (var (exception, status) in cases)
        {
            var r = await Run(exception);

            Assert.That(r.Context.Response.StatusCode, Is.EqualTo(status), exception.GetType().Name);
            Assert.That(r.B.GetProperty("code").GetString(), Is.EqualTo(key));
            Assert.That(r.B.GetProperty("detail").GetString(), Is.EqualTo("message"));
            Assert.That(r.Log.Entries, Is.Empty);
        }
    }

    [Test]
    public async Task KeyedValidationOutcome_CarriesTheErrorsWithCamelCaseFieldNames()
    {
        var errors = new Dictionary<string, string[]> { ["Title"] = ["Enter a title."] };

        var r = await Run(new RequestValidationException(TasksLog.Keys.TaskCreateNotAllowed, "Enter a title.", errors));

        Assert.That(r.Context.Response.StatusCode, Is.EqualTo(400));
        Assert.That(r.Fields, Is.EqualTo(new[] { "type", "title", "status", "detail", "code", "traceId", "errors" }));
        Assert.That(r.B.GetProperty("errors").GetProperty("title")[0].GetString(), Is.EqualTo("Enter a title."));
    }

    [Test]
    public async Task ValidationException_Is400WithoutCode_AndWritesNoEntry()
    {
        var r = await Run(new ValidationException(new System.ComponentModel.DataAnnotations.ValidationResult("Title is required.", ["Title"]), null, null));

        Assert.That(r.Context.Response.StatusCode, Is.EqualTo(400));
        Assert.That(r.B.GetProperty("detail").GetString(), Does.Contain("Title is required."));
        Assert.That(r.B.TryGetProperty("code", out _), Is.False);
        Assert.That(r.B.GetProperty("errors").GetProperty("title")[0].GetString(), Is.EqualTo("Title is required."));
        Assert.That(r.Log.Entries, Is.Empty);
    }

    [Test]
    public async Task FluentValidationException_Is400WithGroupedErrors()
    {
        var r = await Run(new FluentValidation.ValidationException([new ValidationFailure("Title", "Enter a title.")]));

        Assert.That(r.Context.Response.StatusCode, Is.EqualTo(400));
        Assert.That(r.B.GetProperty("detail").GetString(), Is.EqualTo("Enter a title."));
        Assert.That(r.B.GetProperty("errors").GetProperty("title")[0].GetString(), Is.EqualTo("Enter a title."));
    }

    [TestCase(typeof(KeyNotFoundException), 404, "Not Found", "Resource not found.")]
    [TestCase(typeof(UnauthorizedAccessException), 403, "Forbidden", "Access denied.")]
    public async Task NotWrappedRejections_KeepTheirStatus_AndWriteTheRejectedPlatformEvent(Type type, int status, string title, string detail)
    {
        var r = await Run((Exception)Activator.CreateInstance(type, "secret internal text")!);

        Assert.That(r.Context.Response.StatusCode, Is.EqualTo(status));
        Assert.That(r.B.GetProperty("title").GetString(), Is.EqualTo(title));
        Assert.That(r.B.GetProperty("detail").GetString(), Is.EqualTo(detail));
        Assert.That(r.B.TryGetProperty("code", out _), Is.False);
        var entry = r.Log.Entries.Single();
        Assert.That(entry.LogKey, Is.EqualTo(PlatformLog.Keys.PlatformRequestRejected));
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(entry["ExceptionType"], Is.EqualTo(type.FullName));
        Assert.That(entry["Route"], Is.EqualTo(Route));
        Assert.That(entry["TopFrame"], Is.Not.Null);
    }

    [Test]
    public async Task UnhandledException_Is500WithTheGenericMessage_AndWritesTheUnhandledPlatformEventWithTheException()
    {
        var exception = new InvalidOperationException("connection string Server=db;Password=hunter2");

        var r = await Run(exception);

        Assert.That(r.Context.Response.StatusCode, Is.EqualTo(500));
        Assert.That(r.Fields, Is.EqualTo(new[] { "type", "title", "status", "detail", "traceId" }));
        Assert.That(r.B.GetProperty("detail").GetString(), Is.EqualTo("An unexpected error occurred."));
        Assert.That(r.B.GetProperty("title").GetString(), Is.EqualTo("Internal Server Error"));
        var entry = r.Log.Entries.Single();
        Assert.That(entry.LogKey, Is.EqualTo(PlatformLog.Keys.PlatformRequestUnhandled));
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
        Assert.That(entry.Exception, Is.SameAs(exception));
        Assert.That(entry["ExceptionType"], Is.EqualTo("System.InvalidOperationException"));
        Assert.That(entry["Route"], Is.EqualTo(Route));
    }

    [Test]
    public async Task ExceptionDetail_IsReturnedOnlyInDevelopment()
    {
        var exception = new InvalidOperationException("boom");

        var production = await Run(exception, "Production");
        var development = await Run(exception, "Development");

        Assert.That(production.B.TryGetProperty("exception", out _), Is.False);
        Assert.That(production.Context.Response.Body.Length, Is.Not.Zero);
        var info = development.B.GetProperty("exception");
        Assert.That(info.GetProperty("type").GetString(), Is.EqualTo("System.InvalidOperationException"));
        Assert.That(info.GetProperty("message").GetString(), Is.EqualTo("boom"));
        Assert.That(development.B.GetProperty("detail").GetString(), Is.EqualTo("An unexpected error occurred."));
    }

    [Test]
    public async Task CancelledRequest_HasNoBodyAndWritesNoEntry()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var r = await Run(new OperationCanceledException(cts.Token), aborted: cts.Token);

        Assert.That(r.Body, Is.Null);
        Assert.That(r.Log.Entries, Is.Empty);
    }

    [Test]
    public async Task OperationCanceledWithoutAbortedRequest_IsUnhandled()
    {
        var r = await Run(new OperationCanceledException());

        Assert.That(r.Context.Response.StatusCode, Is.EqualTo(500));
    }

    [Test]
    public async Task TraceId_IsAlwaysPresent_AndEqualsTheTraceIdOfTheIncomingTraceparent()
    {
        foreach (var exception in new Exception[]
                 {
                     new OutcomeException(TasksLog.Keys.TaskCreateNotAllowed, 403, "x"), new KeyNotFoundException(),
                     new ValidationException("x"), new InvalidOperationException()
                 })
        {
            var r = await Run(exception);
            Assert.That(r.B.GetProperty("traceId").GetString(), Is.EqualTo("4bf92f3577b34da6a3ce929d0e0e4736"), exception.GetType().Name);
        }
    }

    [Test]
    public async Task TraceId_IsAValid32HexIdEvenWithoutAnActivity()
    {
        _activity.Stop();
        Activity.Current = null;

        var r = await Run(new InvalidOperationException());

        Assert.That(r.B.GetProperty("traceId").GetString(), Does.Match("^[0-9a-f]{32}$"));
        _activity = new Activity("again");
        _activity.Start();
    }

    [Test]
    public async Task TheRateLimiterResponseShape_IsTheSameProblemDocument()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await ProblemResponse.WriteAsync(context, ProblemResponse.Create(429, "Too many requests. Please try again later."));

        context.Response.Body.Position = 0;
        var body = JsonDocument.Parse(context.Response.Body).RootElement;
        Assert.That(context.Response.StatusCode, Is.EqualTo(429));
        Assert.That(context.Response.ContentType, Is.EqualTo("application/problem+json"));
        Assert.That(body.GetProperty("title").GetString(), Is.EqualTo("Too Many Requests"));
        Assert.That(body.GetProperty("detail").GetString(), Is.EqualTo("Too many requests. Please try again later."));
        Assert.That(body.GetProperty("traceId").GetString(), Is.EqualTo("4bf92f3577b34da6a3ce929d0e0e4736"));
    }
}
