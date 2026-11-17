using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Logging;

/// <summary>
/// Writes every entry as one JSON object per line in the format of operations.md 3.4.
/// No external library; configuration in <see cref="FileLoggerOptions"/>.
/// </summary>
[ProviderAlias("JsonFile")]
public sealed class JsonFileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    public const string ServiceName = "myapp-api";
    private const string OriginalFormatKey = "{OriginalFormat}";
    private const int MaxFrames = 20;
    private const int MaxInnerDepth = 3;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        SkipValidation = true
    };

    private readonly FileLoggerOptions _options;
    private readonly string _environment;
    private readonly string _version;
    private readonly string _host;
    private readonly TimeProvider _time;
    private readonly RollingJsonLineFile _file;
    private IExternalScopeProvider? _scopes;

    /// <param name="options">Sink settings.</param>
    /// <param name="contentRoot">Base folder for a relative <see cref="FileLoggerOptions.Directory"/>.</param>
    /// <param name="environment">Host environment name.</param>
    /// <param name="version">Assembly informational version.</param>
    /// <param name="time">Clock; the system clock when null.</param>
    public JsonFileLoggerProvider(FileLoggerOptions options, string contentRoot, string environment, string version,
        TimeProvider? time = null)
    {
        _options = options;
        _environment = environment;
        _version = version;
        _host = Environment.MachineName;
        _time = time ?? TimeProvider.System;

        var directory = Path.IsPathRooted(options.Directory)
            ? options.Directory
            : Path.Combine(contentRoot, options.Directory);
        _file = new RollingJsonLineFile(directory, options.FilePrefix, options.MaxFileSizeMB * 1024L * 1024L,
            options.RetainedFiles, _time);
    }

    public ILogger CreateLogger(string categoryName) => new JsonFileLogger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose() => _file.Dispose();

    private bool IsEnabled(LogLevel level) => level != LogLevel.None && level >= _options.MinimumLevel;

    private void Write<TState>(string category, LogLevel level, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(level))
            return;

        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var json = new Utf8JsonWriter(buffer, WriterOptions))
        {
            json.WriteStartObject();
            json.WriteString("Timestamp", _time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
            json.WriteString("Level", level.ToString());
            json.WriteString("Category", category);
            json.WriteNumber("EventId", eventId.Id);
            if (!string.IsNullOrEmpty(eventId.Name))
                json.WriteString("LogKey", eventId.Name);

            string? template = null;
            var properties = new List<KeyValuePair<string, string>>();
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                foreach (var (key, value) in values)
                {
                    if (key == OriginalFormatKey)
                        template = Convert.ToString(value, CultureInfo.InvariantCulture);
                    else
                        AddProperty(properties, key, value);
                }
            }

            _scopes?.ForEachScope(static (scope, list) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> scopeValues)
                {
                    foreach (var (key, value) in scopeValues)
                    {
                        if (key != OriginalFormatKey)
                            AddProperty(list, key, value);
                    }
                }
            }, properties);

            if (!string.IsNullOrEmpty(template))
                json.WriteString("MessageTemplate", template);
            json.WriteString("Message", formatter(state, exception));

            var activity = Activity.Current;
            if (activity is not null && activity.TraceId != default)
            {
                json.WriteString("TraceId", activity.TraceId.ToHexString());
                if (activity.SpanId != default)
                    json.WriteString("SpanId", activity.SpanId.ToHexString());
            }

            if (properties.Count > 0)
            {
                json.WriteStartObject("Properties");
                foreach (var (key, value) in properties)
                    json.WriteString(key, value);
                json.WriteEndObject();
            }

            if (exception is not null)
            {
                json.WritePropertyName("Exception");
                WriteException(json, exception, 0);
            }

            json.WriteString("Service", ServiceName);
            if (!string.IsNullOrEmpty(_version))
                json.WriteString("Version", _version);
            json.WriteString("Host", _host);
            if (!string.IsNullOrEmpty(_environment))
                json.WriteString("Environment", _environment);
            json.WriteEndObject();
        }

        buffer.Write("\n"u8);
        _file.Write(buffer.WrittenSpan);
    }

    private static void AddProperty(List<KeyValuePair<string, string>> list, string key, object? value)
    {
        if (value is null || list.Exists(p => p.Key == key))
            return;
        list.Add(new KeyValuePair<string, string>(key, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty));
    }

    private static void WriteException(Utf8JsonWriter json, Exception exception, int depth)
    {
        json.WriteStartObject();
        json.WriteString("Type", exception.GetType().FullName ?? exception.GetType().Name);
        json.WriteString("Message", exception.Message);

        var frames = new StackTrace(exception, false).GetFrames()
            .Select(f => f.GetMethod())
            .Where(m => m is not null)
            .Take(MaxFrames)
            .Select(m => ExceptionSummary.FormatFrame(m!))
            .ToList();
        if (frames.Count > 0)
        {
            json.WriteStartArray("Frames");
            foreach (var frame in frames)
                json.WriteStringValue(frame);
            json.WriteEndArray();
        }

        if (exception.InnerException is not null && depth < MaxInnerDepth)
        {
            json.WritePropertyName("Inner");
            WriteException(json, exception.InnerException, depth + 1);
        }

        json.WriteEndObject();
    }

    private sealed class JsonFileLogger(JsonFileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider._scopes?.Push(state);

        public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            try
            {
                provider.Write(category, logLevel, eventId, state, exception, formatter);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A logger never throws into the caller.
            }
        }
    }
}
