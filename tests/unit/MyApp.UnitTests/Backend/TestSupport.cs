using System.Collections;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyApp.UnitTests.Backend;

/// <summary>A clock the test moves by hand.</summary>
internal sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
    public void Set(DateTimeOffset value) => _now = value;
}

internal sealed class TestHostEnvironment(string name) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "MyApp.Tests";
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed record LogEntry(LogLevel Level, EventId EventId, IReadOnlyDictionary<string, object?> State, Exception? Exception, string Message)
{
    public string? LogKey => EventId.Name;
    public object? this[string name] => State.TryGetValue(name, out var v) ? v : null;
}

/// <summary>Records every entry with its structured state.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var values = new Dictionary<string, object?>();
        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            foreach (var (key, value) in pairs)
                values[key] = value;
        }
        Entries.Add(new LogEntry(logLevel, eventId, values, exception, formatter(state, exception)));
    }
}

internal sealed class CapturingLoggerFactory : ILoggerFactory
{
    public CapturingLogger<object> Logger { get; } = new();
    public void AddProvider(ILoggerProvider provider) { }
    public ILogger CreateLogger(string categoryName) => Logger;
    public void Dispose() { }
}
