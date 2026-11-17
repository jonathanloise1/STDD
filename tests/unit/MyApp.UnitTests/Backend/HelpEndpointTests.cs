extern alias WebApi;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using MyApp.Application.Help;
using MyApp.Application.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Reflection;
using WebApi::MyApp.WebApi.ClientEvents;
using WebApi::MyApp.WebApi.Controllers;
using WebApi::MyApp.WebApi.Filters;

namespace MyApp.UnitTests.Backend;

/// <summary>The routes, the authorization and the log keys of the help endpoints (models.md 7.4, operations.md 3.6).</summary>
[TestFixture]
[Property("UserStory", "US-HELP-04")]
[Property("UserStory", "US-HELP-05")]
[Property("UserStory", "US-HELP-07")]
public class HelpEndpointTests
{
    private static MethodInfo Action(string name) => typeof(HelpController).GetMethod(name)!;

    [TestCase(nameof(HelpController.Status), "GET", "status")]
    [TestCase(nameof(HelpController.Answer), "POST", "answer")]
    [TestCase(nameof(HelpController.RecordGap), "POST", "gaps")]
    [TestCase(nameof(HelpController.ListGaps), "GET", "gaps")]
    public void TheRoutesAreTheOnesOfModelsSevenFour(string action, string verb, string template)
    {
        var method = Action(action);
        var http = method.GetCustomAttributes().OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Single();

        Assert.That(http.HttpMethods.Single(), Is.EqualTo(verb));
        Assert.That(http.Template, Is.EqualTo(template));
        Assert.That(typeof(HelpController).GetCustomAttribute<RouteAttribute>()!.Template, Is.EqualTo("api/help"));
    }

    [Test]
    public void EveryRouteNeedsASignedInUserAndIsRateLimitedPerUser()
    {
        var authorize = typeof(AuthenticatedControllerBase).GetCustomAttribute<AuthorizeAttribute>()!;
        var rate = typeof(AuthenticatedControllerBase).GetCustomAttribute<EnableRateLimitingAttribute>()!;

        Assert.That(typeof(HelpController).BaseType, Is.EqualTo(typeof(AuthenticatedControllerBase)));
        Assert.That(authorize.Policy, Is.EqualTo("RequireAuthenticatedUser"));
        Assert.That(rate.PolicyName, Is.EqualTo("DefaultPerUser"));
        Assert.That(typeof(HelpController).GetCustomAttribute<AllowAnonymousAttribute>(), Is.Null);
        Assert.That(typeof(HelpController).GetMethods().SelectMany(m => m.GetCustomAttributes<AllowAnonymousAttribute>()), Is.Empty);
    }

    [Test]
    public void TheDownloadFailureIsTheKeyOfTheServiceUnavailableFilter()
    {
        var attribute = Action(nameof(HelpController.ListGaps)).GetCustomAttribute<ServiceUnavailableAttribute>()!;

        Assert.That(attribute.Key, Is.EqualTo(HelpLog.Keys.HelpDownloadFailed));
        var logger = new CapturingLogger<object>();
        Assert.That(UnavailableOutcomes.Emit(logger, attribute.Key, new TimeoutException(), "System.TimeoutException", "X.Y"), Is.True);
        Assert.That(logger.Entries.Single().Level, Is.EqualTo(LogLevel.Error));
        Assert.That(logger.Entries.Single().LogKey, Is.EqualTo("help.download.failed"));
    }

    [TestCase(HelpLog.Keys.HelpBrowseLoadFailed)]
    [TestCase(HelpLog.Keys.HelpSearchUnavailable)]
    [TestCase(HelpLog.Keys.HelpOpenUnavailable)]
    [TestCase(HelpLog.Keys.HelpAskUnavailable)]
    public void BrowserOnlyFailures_AreReportedThroughClientEventsAtError(string key)
    {
        var factory = new CapturingLoggerFactory();
        var dispatcher = ClientEventDispatcher.ForApplication(factory);

        Assert.That(dispatcher.TryDispatch(key, "/tasks/6f1c2b1e-3c1d-4f6a-9c4e-0123456789ab?x=1"), Is.True);

        var entry = factory.Logger.Entries.Single();
        Assert.That(entry.LogKey, Is.EqualTo(key));
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
        Assert.That(entry["Route"], Is.EqualTo("/tasks/{id}"));
    }

    [TestCase(HelpLog.Keys.HelpAnswerDeclined)]
    [TestCase(HelpLog.Keys.HelpAnswerTimeout)]
    [TestCase(HelpLog.Keys.HelpDownloadFailed)]
    public void ServerSideKeys_CannotBeTriggeredFromTheBrowser(string key)
    {
        var factory = new CapturingLoggerFactory();

        Assert.That(ClientEventDispatcher.ForApplication(factory).TryDispatch(key, "/x"), Is.False);
        Assert.That(factory.Logger.Entries, Is.Empty);
    }

    [Test]
    public void TheSeverityOfEveryHelpKeyIsTheOneOfItsTestCase()
    {
        var expected = new Dictionary<string, LogLevel>
        {
            [HelpLog.Keys.HelpBrowseLoadFailed] = LogLevel.Error,
            [HelpLog.Keys.HelpSearchUnavailable] = LogLevel.Error,
            [HelpLog.Keys.HelpOpenUnavailable] = LogLevel.Error,
            [HelpLog.Keys.HelpAskUnavailable] = LogLevel.Error,
            [HelpLog.Keys.HelpAnswerDeclined] = LogLevel.Information,
            [HelpLog.Keys.HelpAnswerTimeout] = LogLevel.Warning,
            [HelpLog.Keys.HelpDownloadFailed] = LogLevel.Error
        };

        var methods = typeof(HelpLog).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(m => m.GetCustomAttribute<LoggerMessageAttribute>()).Where(a => a is not null).ToList();

        Assert.That(methods.Select(a => a!.EventName), Is.EquivalentTo(expected.Keys));
        foreach (var attribute in methods)
            Assert.That(attribute!.Level, Is.EqualTo(expected[attribute.EventName!]), attribute.EventName);
        Assert.That(methods.Select(a => a!.EventId), Is.Unique);
    }

    [Test]
    public void TheStreamLines_AreOneJsonObjectEachWithoutAbsentValues()
    {
        var delta = HelpController.ToLine(new HelpAnswerEvent(HelpAnswerEvent.Delta, Text: "Click \"New\"\n"));
        var done = HelpController.ToLine(new HelpAnswerEvent(HelpAnswerEvent.Done, Text: "t", Outcome: "answered", Cited: [1, 2], Body: "b", Model: "m", Device: "gpu"));
        var error = HelpController.ToLine(new HelpAnswerEvent(HelpAnswerEvent.Error, Reason: "timeout"));

        Assert.That(delta, Does.EndWith("\n"));
        Assert.That(delta.TrimEnd('\n'), Does.Not.Contain("\n"));
        Assert.That(JObject.Parse(delta), Is.EqualTo(new JObject { ["type"] = "delta", ["text"] = "Click \"New\"\n" }));
        Assert.That(JObject.Parse(done).ToString(Formatting.None),
            Is.EqualTo("{\"type\":\"done\",\"text\":\"t\",\"outcome\":\"answered\",\"cited\":[1,2],\"body\":\"b\",\"model\":\"m\",\"device\":\"gpu\"}"));
        Assert.That(JObject.Parse(error), Is.EqualTo(new JObject { ["type"] = "error", ["reason"] = "timeout" }));
    }

    [Test]
    public void TheBodies_UseTheSnakeCaseNamesOfTheDocuments()
    {
        var request = JsonConvert.DeserializeObject<MyApp.Application.DTOs.Help.HelpAnswerRequest>(
            "{\"lang\":\"en\",\"question\":\"q\",\"section_ids\":[\"a#b\"],\"pack_sha256\":\"x\"}")!;
        var gap = JsonConvert.DeserializeObject<MyApp.Application.DTOs.Help.HelpGapDto>(
            "{\"schema_version\":1,\"recorded_at\":\"2026-11-06T10:15:00Z\",\"lang\":\"en\",\"route\":\"/tasks\",\"question\":\"q\",\"outcome\":\"no_match\",\"tier\":\"retrieval\"," +
            "\"pack\":{\"project_version\":\"0.1.0\",\"sha256\":\"h\"},\"top_sections\":[\"a#b\"],\"coverage\":0.22}")!;

        Assert.That(request.SectionIds, Is.EqualTo(new[] { "a#b" }));
        Assert.That(request.PackSha256, Is.EqualTo("x"));
        Assert.That(gap.TopSections, Is.EqualTo(new[] { "a#b" }));
        Assert.That(gap.Pack!.ProjectVersion, Is.EqualTo("0.1.0"));
        Assert.That(gap.RecordedAt!.Value.ToUniversalTime(), Is.EqualTo(new DateTime(2026, 11, 6, 10, 15, 0, DateTimeKind.Utc)));

        var status = JObject.Parse(JsonConvert.SerializeObject(new MyApp.Application.DTOs.Help.HelpStatusDto()));
        Assert.That(status.Properties().Select(p => p.Name), Is.EquivalentTo(new[]
            { "order", "configured_order", "compatible", "reason", "pack", "model", "browser", "server", "gaps_enabled" }));
    }
}
