using Microsoft.Extensions.Logging;
using MyApp.Application.Logging;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Logging;
using NUnit.Framework;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace MyApp.UnitTests.Backend;

[TestFixture]
public class LogKeyProcessorTests
{
    private sealed class Capture : BaseExporter<LogRecord>
    {
        public List<Dictionary<string, object?>> Records { get; } = [];

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (var record in batch)
            {
                var values = new Dictionary<string, object?>();
                foreach (var (key, value) in record.Attributes ?? [])
                    values[key] = value;
                Records.Add(values);
            }
            return ExportResult.Success;
        }
    }

    private static (ILoggerFactory Factory, Capture Capture) Create()
    {
        var capture = new Capture();
        var factory = LoggerFactory.Create(b => b.AddOpenTelemetry(o =>
        {
            o.ParseStateValues = true;
            o.IncludeFormattedMessage = true;
            o.AddProcessor(new LogKeyProcessor());
            o.AddProcessor(new SimpleLogRecordExportProcessor(capture));
        }));
        return (factory, capture);
    }

    [Test]
    public void KeyedEntry_CarriesTheLogKeyAttribute_AndKeepsTheTemplateParameters()
    {
        var (factory, capture) = Create();
        var orgId = Guid.NewGuid();

        factory.CreateLogger("Cat").TaskCreateNotAllowed(OrganizationRole.Viewer, Guid.NewGuid(), orgId);

        var record = capture.Records.Single();
        Assert.That(record["LogKey"], Is.EqualTo(TasksLog.Keys.TaskCreateNotAllowed));
        Assert.That(record["OrganizationId"], Is.EqualTo(orgId));
        Assert.That(record.ContainsKey("{OriginalFormat}"), Is.True);
        Assert.That(record.Keys.Count(k => k == "LogKey"), Is.EqualTo(1));
    }

    [Test]
    public void UnkeyedEntry_HasNoLogKeyAttribute()
    {
        var (factory, capture) = Create();

        factory.CreateLogger("Cat").LogInformation("Hello {Count}", 1);

        Assert.That(capture.Records.Single().ContainsKey("LogKey"), Is.False);
    }

    [Test]
    public void PlatformEvent_CarriesItsEventNameAsLogKey()
    {
        var (factory, capture) = Create();

        factory.CreateLogger("Cat").PlatformRequestRateLimited("DefaultPerUser");

        Assert.That(capture.Records.Single()["LogKey"], Is.EqualTo(PlatformLog.Keys.PlatformRequestRateLimited));
    }
}
