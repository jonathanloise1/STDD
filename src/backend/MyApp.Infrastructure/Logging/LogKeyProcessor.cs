using OpenTelemetry;
using OpenTelemetry.Logs;

namespace MyApp.Infrastructure.Logging;

/// <summary>
/// Carries the log key to Application Insights (operations.md 3.3): when the entry has an event name,
/// adds the attribute <see cref="AttributeName"/> with that name. Application Insights stores it in
/// <c>Properties.LogKey</c>. It must be registered before the exporter processors.
/// </summary>
public sealed class LogKeyProcessor : BaseProcessor<LogRecord>
{
    public const string AttributeName = "LogKey";

    public override void OnEnd(LogRecord data)
    {
        var name = data.EventId.Name;
        if (string.IsNullOrEmpty(name))
            return;

        var attributes = new List<KeyValuePair<string, object?>>();
        if (data.Attributes is { } existing)
        {
            foreach (var item in existing)
            {
                if (item.Key == AttributeName)
                    return; // already carried
                attributes.Add(item);
            }
        }

        attributes.Add(new KeyValuePair<string, object?>(AttributeName, name));
        data.Attributes = attributes;
    }
}
