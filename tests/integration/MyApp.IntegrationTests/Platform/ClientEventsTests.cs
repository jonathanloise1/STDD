using System.Reflection;
using Microsoft.Extensions.Logging;
using MyApp.Application.Logging;
using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Platform;

/// <summary>
/// The traces a user meets in the browser only reach the backend through <c>POST /api/client-events</c>
/// (operations.md 3.6): a known key is accepted from anyone, an unknown key is refused and writes nothing.
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("UserStory", "US-AUTH-06")]
public class ClientEventsTests
{
    /// <summary>A key some log definition marks [ClientReported]; which one is not this test's concern.</summary>
    private static string AClientReportedKey() =>
        typeof(ClientReportedAttribute).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(ClientReportedAttribute).Namespace && t.IsAbstract && t.IsSealed)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<ClientReportedAttribute>() is not null)
            .Select(m => m.GetCustomAttribute<LoggerMessageAttribute>()!.EventName!)
            .First();

    [Test]
    [Description("A known key is accepted from a caller who is not signed in")]
    public async Task KnownKey_IsAcceptedAnonymously()
    {
        var response = await Api.Post("/api/client-events", new { key = AClientReportedKey(), route = "/does-not-exist" });

        ApiAssert.IsSuccess(response, 204);
    }

    [Test]
    [Description("The route may carry ids and a query string; it is accepted all the same")]
    public async Task Route_WithIdsAndQuery_IsAccepted()
    {
        var response = await Api.Post(
            "/api/client-events",
            new { key = AClientReportedKey(), route = $"/tasks/{Guid.NewGuid()}/42?search=x#top" });

        ApiAssert.IsSuccess(response, 204);
    }

    [Test]
    [Description("An unknown key is refused with 400 and the problem document")]
    public async Task UnknownKey_IsRefused()
    {
        var response = await Api.Post("/api/client-events", new { key = "no.such.key", route = "/" });

        ApiAssert.IsProblem(response, 400, detail: "Unknown client event.");
    }

    [Test]
    [Description("A key that is logged by the backend itself is not accepted from the browser")]
    public async Task ServerKey_IsRefused()
    {
        var response = await Api.Post("/api/client-events", new { key = "task.create.not_allowed", route = "/" });

        ApiAssert.IsProblem(response, 400, detail: "Unknown client event.");
    }

    [Test]
    [Description("A body with no key is refused")]
    public async Task MissingKey_IsRefused()
    {
        var response = await Api.Post("/api/client-events", new { route = "/" });

        ApiAssert.IsProblem(response, 400);
    }
}
