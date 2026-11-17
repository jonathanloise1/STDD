extern alias WebApi;

using Microsoft.Extensions.Logging;
using MyApp.Application.Logging;
using WebApi::MyApp.WebApi.ClientEvents;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

public static partial class FixtureClientLog
{
    public static class Keys
    {
        public const string FixturePageNotFound = "fixture.nav.page_not_found";
        public const string FixtureNotReported = "fixture.nav.not_reported";
    }

    [ClientReported]
    [LoggerMessage(EventId = 1, EventName = Keys.FixturePageNotFound, Level = LogLevel.Warning,
        Message = "Page not found: {Route}")]
    public static partial void FixturePageNotFound(this ILogger logger, string route);

    [LoggerMessage(EventId = 2, EventName = Keys.FixtureNotReported, Level = LogLevel.Warning,
        Message = "Not reported by clients: {Route}")]
    public static partial void FixtureNotReported(this ILogger logger, string route);
}

[TestFixture]
public class ClientEventDispatcherTests
{
    private static (ClientEventDispatcher Dispatcher, CapturingLogger<object> Log) Create()
    {
        var factory = new CapturingLoggerFactory();
        return (new ClientEventDispatcher([typeof(FixtureClientLog)], factory), factory.Logger);
    }

    [Test]
    public void KnownKey_WritesTheKeyedEntryWithTheNormalizedRoute()
    {
        var (dispatcher, log) = Create();

        var ok = dispatcher.TryDispatch(FixtureClientLog.Keys.FixturePageNotFound, "/tasks/6f1c2b1e-3c1d-4f6a-9c4e-0123456789ab/edit?x=1");

        Assert.That(ok, Is.True);
        var entry = log.Entries.Single();
        Assert.That(entry.LogKey, Is.EqualTo(FixtureClientLog.Keys.FixturePageNotFound));
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(entry["Route"], Is.EqualTo("/tasks/{id}/edit"));
    }

    [TestCase("nope.unknown.key")]
    [TestCase(FixtureClientLog.Keys.FixtureNotReported)]
    [TestCase("")]
    [TestCase(null)]
    public void UnknownOrNotClientReportedKey_WritesNothing(string? key)
    {
        var (dispatcher, log) = Create();

        Assert.That(dispatcher.TryDispatch(key, "/x"), Is.False);
        Assert.That(log.Entries, Is.Empty);
    }

    [TestCase("/organizations/42/tasks?page=2#top", "/organizations/{id}/tasks")]
    [TestCase("/a/0b7d4c3e-1111-2222-3333-444455556666", "/a/{id}")]
    [TestCase("/plain/path", "/plain/path")]
    [TestCase("", "/")]
    [TestCase(null, "/")]
    public void Route_IsNormalized(string? input, string expected) =>
        Assert.That(ClientEventDispatcher.NormalizeRoute(input), Is.EqualTo(expected));

    [Test]
    public void Route_IsCutAt200Characters() =>
        Assert.That(ClientEventDispatcher.NormalizeRoute("/" + new string('a', 500)), Has.Length.EqualTo(200));

    [Test]
    public void TheApplicationDefinitions_BuildTheMapWithoutError() =>
        Assert.DoesNotThrow(() => ClientEventDispatcher.ForApplication(new CapturingLoggerFactory()));
}
