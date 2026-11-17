using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyApp.Application.Logging;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Logging;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

[TestFixture]
public class JsonFileLoggerTests
{
    private string _dir = null!;
    private TestTimeProvider _time = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "myapp-logtests-" + Guid.NewGuid().ToString("N"));
        _time = new TestTimeProvider(new DateTimeOffset(2026, 11, 6, 10, 42, 1, 123, TimeSpan.Zero));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private JsonFileLoggerProvider Provider(Action<FileLoggerOptions>? configure = null)
    {
        var options = new FileLoggerOptions { Directory = _dir, FilePrefix = "myapp-api" };
        configure?.Invoke(options);
        return new JsonFileLoggerProvider(options, Path.GetTempPath(), "Production", "1.4.0", _time);
    }

    private string[] Files() => Directory.Exists(_dir)
        ? Directory.GetFiles(_dir).Select(f => Path.GetFileName(f)!).OrderBy(n => n, StringComparer.Ordinal).ToArray()
        : [];

    private static JsonDocument[] Lines(string path) =>
        File.ReadAllText(path).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => JsonDocument.Parse(l)).ToArray();

    [Test]
    public void KeyedEntry_IsWrittenInTheDocumentedFormat()
    {
        var traceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        var activity = new Activity("test");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.SetParentId(traceparent);
        activity.Start();
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        try
        {
            using var provider = Provider();
            provider.CreateLogger("MyApp.Application.Services.TaskItemService")
                .TaskCreateNotAllowed(OrganizationRole.Viewer, userId, orgId);
        }
        finally
        {
            activity.Stop();
        }

        Assert.That(Files(), Is.EqualTo(new[] { "myapp-api-20261106.jsonl" }));
        var bytes = File.ReadAllBytes(Path.Combine(_dir, "myapp-api-20261106.jsonl"));
        Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "no BOM");
        Assert.That(bytes[^1], Is.EqualTo((byte)'\n'));
        Assert.That(Encoding.UTF8.GetString(bytes), Does.Not.Contain("\r"));

        var line = Lines(Path.Combine(_dir, "myapp-api-20261106.jsonl")).Single().RootElement;
        Assert.That(line.EnumerateObject().Select(p => p.Name), Is.EqualTo(new[]
        {
            "Timestamp", "Level", "Category", "EventId", "LogKey", "MessageTemplate", "Message",
            "TraceId", "SpanId", "Properties", "Service", "Version", "Host", "Environment"
        }));
        Assert.That(line.GetProperty("Timestamp").GetString(), Is.EqualTo("2026-11-06T10:42:01.123Z"));
        Assert.That(line.GetProperty("Level").GetString(), Is.EqualTo("Warning"));
        Assert.That(line.GetProperty("Category").GetString(), Is.EqualTo("MyApp.Application.Services.TaskItemService"));
        Assert.That(line.GetProperty("EventId").GetInt32(), Is.EqualTo(1));
        Assert.That(line.GetProperty("LogKey").GetString(), Is.EqualTo(TasksLog.Keys.TaskCreateNotAllowed));
        Assert.That(line.GetProperty("MessageTemplate").GetString(), Does.Contain("{UserId}").And.Contain("{OrganizationId}"));
        Assert.That(line.GetProperty("Message").GetString(), Does.Contain(userId.ToString()).And.Not.Contain("{"));
        Assert.That(line.GetProperty("TraceId").GetString(), Is.EqualTo("4bf92f3577b34da6a3ce929d0e0e4736"));
        Assert.That(line.GetProperty("SpanId").GetString(), Has.Length.EqualTo(16).And.Match("^[0-9a-f]{16}$"));
        var props = line.GetProperty("Properties");
        Assert.That(props.GetProperty("UserId").GetString(), Is.EqualTo(userId.ToString()));
        Assert.That(props.GetProperty("OrganizationId").GetString(), Is.EqualTo(orgId.ToString()));
        Assert.That(props.GetProperty("Role").GetString(), Is.EqualTo("Viewer"));
        Assert.That(props.TryGetProperty("{OriginalFormat}", out _), Is.False);
        Assert.That(line.GetProperty("Service").GetString(), Is.EqualTo("myapp-api"));
        Assert.That(line.GetProperty("Version").GetString(), Is.EqualTo("1.4.0"));
        Assert.That(line.GetProperty("Environment").GetString(), Is.EqualTo("Production"));
        Assert.That(line.GetProperty("Host").GetString(), Is.EqualTo(Environment.MachineName));
    }

    [Test]
    public void UnkeyedEntryWithoutActivity_OmitsAbsentFieldsInsteadOfNull()
    {
        Assert.That(Activity.Current, Is.Null);
        using (var provider = Provider())
            provider.CreateLogger("Cat").LogInformation("Hello {Count}", 3);

        var line = Lines(Path.Combine(_dir, "myapp-api-20261106.jsonl")).Single().RootElement;
        var names = line.EnumerateObject().Select(p => p.Name).ToList();
        Assert.That(names, Does.Not.Contain("LogKey"));
        Assert.That(names, Does.Not.Contain("TraceId"));
        Assert.That(names, Does.Not.Contain("SpanId"));
        Assert.That(names, Does.Not.Contain("Exception"));
        Assert.That(line.EnumerateObject().Any(p => p.Value.ValueKind == JsonValueKind.Null), Is.False);
        Assert.That(line.GetProperty("Properties").GetProperty("Count").GetString(), Is.EqualTo("3"));
    }

    [Test]
    public void ScopeValues_AreAddedToProperties_AndUnicodeIsKept()
    {
        using (var provider = Provider())
        {
            var scopes = new Microsoft.Extensions.Logging.LoggerExternalScopeProvider();
            provider.SetScopeProvider(scopes);
            var logger = provider.CreateLogger("Cat");
            using (logger.BeginScope(new Dictionary<string, object?> { ["OrganizationId"] = "org-1" }))
                logger.LogInformation("Attività {Count}", 1);
        }

        var path = Path.Combine(_dir, "myapp-api-20261106.jsonl");
        Assert.That(File.ReadAllText(path), Does.Contain("Attività"));
        Assert.That(Lines(path).Single().RootElement.GetProperty("Properties").GetProperty("OrganizationId").GetString(), Is.EqualTo("org-1"));
    }

    [Test]
    public void Exception_IsWrittenWithTypeMessageFramesAndInnerUpToDepthThree()
    {
        var ex = Deep(5);
        using (var provider = Provider())
            provider.CreateLogger("Cat").LogError(ex, "Failed");

        var root = Lines(Path.Combine(_dir, "myapp-api-20261106.jsonl")).Single().RootElement.GetProperty("Exception");
        Assert.That(root.GetProperty("Type").GetString(), Is.EqualTo("System.InvalidOperationException"));
        Assert.That(root.GetProperty("Message").GetString(), Is.EqualTo("level 5"));
        var frames = root.GetProperty("Frames").EnumerateArray().Select(f => f.GetString()).ToList();
        Assert.That(frames, Has.Count.InRange(1, 20));
        Assert.That(frames[0], Is.EqualTo("MyApp.UnitTests.Backend.JsonFileLoggerTests.Deep"));

        var depth = 0;
        var current = root;
        while (current.TryGetProperty("Inner", out var inner))
        {
            depth++;
            current = inner;
        }
        Assert.That(depth, Is.EqualTo(3));
    }

    private static Exception Deep(int level)
    {
        try
        {
            throw level == 0 ? new InvalidOperationException("level 0") : new InvalidOperationException("level " + level, Deep(level - 1));
        }
        catch (Exception e)
        {
            return e;
        }
    }

    [Test]
    public void EntriesBelowTheMinimumLevel_AreNotWritten()
    {
        using (var provider = Provider(o => o.MinimumLevel = LogLevel.Warning))
        {
            var logger = provider.CreateLogger("Cat");
            logger.LogInformation("skipped");
            Assert.That(logger.IsEnabled(LogLevel.Information), Is.False);
            logger.LogWarning("kept");
        }

        var lines = File.ReadAllLines(Path.Combine(_dir, "myapp-api-20261106.jsonl"));
        Assert.That(lines, Has.Length.EqualTo(1));
        Assert.That(lines[0], Does.Contain("kept"));
    }

    [Test]
    public void Rolling_StartsANewFileAfterTheSizeLimit_WithAnIncreasingNumber()
    {
        using (var provider = Provider(o => o.MaxFileSizeMB = 1))
        {
            var logger = provider.CreateLogger("Cat");
            var big = new string('x', 200_000);
            for (var i = 0; i < 7; i++)
                logger.LogInformation("{Payload}", big);
        }

        Assert.That(Files(), Is.EqualTo(new[]
        {
            "myapp-api-20261106-1.jsonl", "myapp-api-20261106-2.jsonl", "myapp-api-20261106-3.jsonl", "myapp-api-20261106.jsonl"
        }));
        foreach (var file in Directory.GetFiles(_dir))
            Assert.That(new FileInfo(file).Length, Is.LessThanOrEqualTo(1024 * 1024));
        Assert.That(Directory.GetFiles(_dir).Sum(f => Lines(f).Length), Is.EqualTo(7), "no entry lost or split");
    }

    [Test]
    public void Rolling_ContinuesTheLatestFileAfterARestart()
    {
        using (var provider = Provider())
            provider.CreateLogger("Cat").LogInformation("first");
        using (var provider = Provider())
            provider.CreateLogger("Cat").LogInformation("second");

        Assert.That(Files(), Is.EqualTo(new[] { "myapp-api-20261106.jsonl" }));
        Assert.That(File.ReadAllLines(Path.Combine(_dir, "myapp-api-20261106.jsonl")), Has.Length.EqualTo(2));
    }

    [Test]
    public void Rolling_StartsANewFileAtTheUtcDateChange()
    {
        using var provider = Provider();
        var logger = provider.CreateLogger("Cat");
        logger.LogInformation("before");
        _time.Set(new DateTimeOffset(2026, 11, 6, 23, 59, 59, TimeSpan.Zero));
        logger.LogInformation("still the same day");
        _time.Set(new DateTimeOffset(2026, 11, 7, 0, 0, 1, TimeSpan.Zero));
        logger.LogInformation("after");

        Assert.That(Files(), Is.EqualTo(new[] { "myapp-api-20261106.jsonl", "myapp-api-20261107.jsonl" }));
        Assert.That(File.ReadAllLines(Path.Combine(_dir, "myapp-api-20261106.jsonl")), Has.Length.EqualTo(2));
        Assert.That(File.ReadAllLines(Path.Combine(_dir, "myapp-api-20261107.jsonl")), Has.Length.EqualTo(1));
    }

    [Test]
    public void Retention_DeletesTheOldestFilesBeyondTheRetainedCount_AndOnlyFilesOfThePrefix()
    {
        Directory.CreateDirectory(_dir);
        foreach (var day in new[] { "20261101", "20261102", "20261103", "20261104" })
            File.WriteAllText(Path.Combine(_dir, $"myapp-api-{day}.jsonl"), "{}\n");
        File.WriteAllText(Path.Combine(_dir, "myapp-api-20261104-1.jsonl"), "{}\n");
        File.WriteAllText(Path.Combine(_dir, "other-20200101.jsonl"), "{}\n");
        File.WriteAllText(Path.Combine(_dir, "myapp-api-notes.jsonl"), "{}\n");

        using (var provider = Provider(o => o.RetainedFiles = 3))
            provider.CreateLogger("Cat").LogInformation("today");

        Assert.That(Files(), Is.EqualTo(new[]
        {
            "myapp-api-20261104-1.jsonl", "myapp-api-20261104.jsonl", "myapp-api-20261106.jsonl",
            "myapp-api-notes.jsonl", "other-20200101.jsonl"
        }));
    }

    [Test]
    public void FileIsReadableWhileTheSinkHoldsItOpen()
    {
        using var provider = Provider();
        provider.CreateLogger("Cat").LogInformation("live");

        using var reader = new FileStream(Path.Combine(_dir, "myapp-api-20261106.jsonl"), FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var text = new StreamReader(reader);
        Assert.That(text.ReadToEnd(), Does.Contain("live"));
    }

    [Test]
    public void ARelativeDirectory_IsResolvedAgainstTheContentRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "myapp-logtests-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var provider = new JsonFileLoggerProvider(new FileLoggerOptions { Directory = "logs" }, root, "Production", "1.0.0", _time))
                provider.CreateLogger("Cat").LogInformation("x");
            Assert.That(File.Exists(Path.Combine(root, "logs", "myapp-api-20261106.jsonl")), Is.True);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
