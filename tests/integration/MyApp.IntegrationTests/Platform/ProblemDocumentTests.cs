using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Platform;

/// <summary>
/// The shape of every error response (operations.md 4.2) and its link to the request (4.1): a problem
/// document whose <c>traceId</c> is the trace id of the <c>traceparent</c> the caller sent.
/// </summary>
[TestFixture]
[Category("authentication")]
[Category("tasks")]
[Property("UserStory", "US-AUTH-02")]
[Property("UserStory", "US-TASK-01")]
public class ProblemDocumentTests
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
    private static readonly (string Name, string Value) TraceParent = ("traceparent", $"00-{TraceId}-00f067aa0ba902b7-01");

    [Test]
    [Description("A keyed outcome has type, title, status, detail, code and traceId")]
    public async Task KeyedOutcome_HasTheFullShape()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var viewer = await TestWorld.AddMember(organizationId, admin, "Viewer");

        var response = await Api.Post(TestWorld.TasksPath(organizationId), new { title = "Not for a viewer" }, viewer.Id);

        ApiAssert.IsProblem(response, 403, "task.create.not_allowed", "Your role does not allow this change.");
        Assert.That(response.Problem.Title, Is.EqualTo("Forbidden"));
        Assert.That(response.Problem.Errors, Is.Null, "Only validation errors carry errors");
    }

    [Test]
    [Description("A validation error carries the messages by field and no code")]
    public async Task ValidationError_CarriesTheErrorsByField()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();

        var response = await Api.Post(TestWorld.TasksPath(organizationId), new { title = "" }, admin.Id);

        ApiAssert.IsProblem(response, 400);
        Assert.That(response.Problem.Errors, Is.Not.Null.And.ContainKey("title"));
    }

    [Test]
    [Description("A request with no identity is answered with the problem document too")]
    public async Task Unauthorized_IsAProblemDocument()
    {
        var response = await Api.Get("/api/organizations");

        ApiAssert.IsProblem(response, 401, detail: "Authentication is required.");
        Assert.That(response.Problem.Title, Is.EqualTo("Unauthorized"));
    }

    [Test]
    [Description("An address that matches nothing is a 404 problem document")]
    public async Task UnknownAddress_IsAProblemDocument()
    {
        var response = await Api.Get("/api/does-not-exist", Guid.Parse("00000000-0000-0000-0000-000000000001"));

        ApiAssert.IsProblem(response, 404, detail: "Resource not found.");
    }

    [Test]
    [Description("The traceId of an error is the trace id of the traceparent the caller sent")]
    public async Task TraceId_IsTheSentTraceId()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var viewer = await TestWorld.AddMember(organizationId, admin, "Viewer");

        var keyed = await Api.Post(TestWorld.TasksPath(organizationId), new { title = "x" }, viewer.Id, TraceParent);
        var anonymous = await Api.Get("/api/organizations", null, TraceParent);

        Assert.That(keyed.Problem.TraceId, Is.EqualTo(TraceId));
        Assert.That(anonymous.Problem.TraceId, Is.EqualTo(TraceId));
    }

    [Test]
    [Description("Without a traceparent the backend still answers with a trace id of its own")]
    public async Task TraceId_IsPresentWithoutATraceparent()
    {
        var response = await Api.Get("/api/organizations");

        Assert.That(response.Problem.TraceId, Does.Match("^[0-9a-f]{32}$"));
        Assert.That(response.Problem.TraceId, Is.Not.EqualTo(TraceId));
    }

    [Test]
    [Description("A failed operation of the fault switch carries the key as code and the sent trace id")]
    public async Task ServiceUnavailable_CarriesTheCodeAndTheTraceId()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();

        var response = await Api.Get(TestWorld.TasksPath(organizationId), admin.Id, Api.Fault("task.list.unavailable"), TraceParent);

        ApiAssert.IsProblem(response, 503, "task.list.unavailable", "The service did not answer. Try again in a few minutes.");
        Assert.That(response.Problem.TraceId, Is.EqualTo(TraceId));
        Assert.That(response.Problem.Exception, Is.Not.Null, "Development adds the exception");
    }
}
