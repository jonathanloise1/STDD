using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyApp.Monitor.Sources;

/// <summary>
/// The <c>http_health</c> source (operations.md 6.4): polls the backend's <c>/health</c> and emits
/// <c>monitor.api.unreachable</c> after <c>failures_before_signal</c> consecutive failures, and
/// <c>monitor.api.unhealthy</c> for each check that is not Healthy.
/// </summary>
public sealed class HttpHealthSource(string id, IHttpClientFactory http) : PollingSourceBase(id, "http_health")
{
    public const string Category = "MyApp.Monitor.Sources.HttpHealthSource";
    public const string UnreachableEvent = "monitor.api.unreachable";
    public const string UnhealthyEvent = "monitor.api.unhealthy";

    private string _url = "";
    private int _failuresBeforeSignal = 3;
    private int _consecutiveFailures;
    private string _lastStatus = "unknown";

    protected override TimeSpan PollInterval => TimeSpan.FromSeconds(Math.Max(1, Context.GetInt("poll_seconds", 60)));

    protected override Task OnStartAsync(CancellationToken ct)
    {
        var baseUrl = Context.GetString("base_url");
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out _))
            throw new SourceConfigurationException($"source '{Id}': base_url must be an absolute URL");
        _url = baseUrl.TrimEnd('/') + "/health";
        _failuresBeforeSignal = Math.Max(1, Context.GetInt("failures_before_signal", 3));
        return Task.CompletedTask;
    }

    public override Task CommitAsync(SignalBatch batch, CancellationToken ct) => Task.CompletedTask;

    protected override IReadOnlyDictionary<string, string> HealthDetails() => new Dictionary<string, string>
    {
        ["url"] = _url,
        ["consecutive_failures"] = _consecutiveFailures.ToString(CultureInfo.InvariantCulture),
        ["last_status"] = _lastStatus,
    };

    protected override async Task<IReadOnlyList<SignalBatch>> PollAsync(CancellationToken ct)
    {
        var now = Time.GetUtcNow();
        JsonNode? body = null;
        string? failure = null;
        try
        {
            var client = http.CreateClient("monitor-health");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            using var response = await client.GetAsync(_url, timeout.Token).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            try { body = JsonNode.Parse(text); }
            catch (JsonException) { body = null; }
            if (body is not JsonObject) failure = $"the health endpoint answered {(int)response.StatusCode} without a health document";
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { failure = "the health endpoint did not answer in time"; }
        catch (HttpRequestException ex) { failure = ex.Message; }

        var records = new List<LogRecord>();
        if (failure is not null)
        {
            _consecutiveFailures++;
            _lastStatus = "unreachable";
            if (_consecutiveFailures >= _failuresBeforeSignal)
                records.Add(LogRecord.Create(
                    Id, Context.Environment, now, RecordLevel.Critical, Category,
                    $"The API at {_url} is unreachable after {_consecutiveFailures} consecutive failures",
                    eventName: UnreachableEvent,
                    messageTemplate: "The API at {Url} is unreachable after {Failures} consecutive failures",
                    properties: new Dictionary<string, string> { ["Url"] = _url, ["Failures"] = _consecutiveFailures.ToString(CultureInfo.InvariantCulture) }));
        }
        else
        {
            _consecutiveFailures = 0;
            var obj = (JsonObject)body!;
            _lastStatus = MonitorJson.ScalarText(obj["status"]) ?? "unknown";
            var checks = (obj["checks"] as JsonArray) ?? [];
            foreach (var c in checks)
            {
                var status = MonitorJson.ScalarText(c?["status"]) ?? "Unknown";
                if (string.Equals(status, "Healthy", StringComparison.OrdinalIgnoreCase)) continue;
                var name = MonitorJson.ScalarText(c?["name"]) ?? "unknown";
                records.Add(Unhealthy(now, name, status));
            }
            if (checks.Count == 0 && !string.Equals(_lastStatus, "Healthy", StringComparison.OrdinalIgnoreCase))
                records.Add(Unhealthy(now, "overall", _lastStatus));
        }
        return records.Count == 0 ? [] : [new SignalBatch(Id, records, null)];
    }

    private LogRecord Unhealthy(DateTimeOffset now, string check, string status) => LogRecord.Create(
        Id, Context.Environment, now, RecordLevel.Error, Category,
        $"Health check {check} is {status}",
        eventName: UnhealthyEvent,
        messageTemplate: "Health check {Check} is {Status}",
        properties: new Dictionary<string, string> { ["Check"] = check, ["Status"] = status });
}

public sealed class HttpHealthSourceFactory(IHttpClientFactory http) : ISignalSourceFactory
{
    public string Kind => "http_health";
    public ISignalSource Create(string sourceId) => new HttpHealthSource(sourceId, http);
}

/// <summary>
/// The <c>world</c> source (operations.md 6.4, 11.6): reads a simulated world directory once. For tests and training only;
/// a profile never lists it (rule O08).
/// </summary>
public sealed class WorldSource(string id) : PollingSourceBase(id, "world")
{
    private string _file = "";
    private bool _done;

    protected override TimeSpan PollInterval => TimeSpan.FromSeconds(1);

    protected override Task OnStartAsync(CancellationToken ct)
    {
        var directory = Context.GetString("directory") ?? throw new SourceConfigurationException($"source '{Id}': directory is missing");
        _file = Path.Combine(directory, "logs.jsonl");
        return Task.CompletedTask;
    }

    public override Task CommitAsync(SignalBatch batch, CancellationToken ct) => Task.CompletedTask;

    protected override Task<IReadOnlyList<SignalBatch>> PollAsync(CancellationToken ct)
    {
        if (_done || !File.Exists(_file)) return Task.FromResult<IReadOnlyList<SignalBatch>>([]);
        _done = true;
        var records = File.ReadLines(_file)
            .Where(l => l.Length > 0)
            .Select(l => JsonlRecordParser.Parse(l, Id, Context.Environment))
            .OfType<LogRecord>()
            .OrderBy(r => r.Timestamp)
            .ToList();
        return Task.FromResult<IReadOnlyList<SignalBatch>>([new SignalBatch(Id, records, null)]);
    }

    /// <summary>The world is read once: a reader that wants all records calls this instead of looping.</summary>
    public async Task<IReadOnlyList<LogRecord>> ReadAllAsync(CancellationToken ct)
    {
        var result = await RunOnceAsync(ct).ConfigureAwait(false);
        return result.Batches.SelectMany(b => b.Records).ToList();
    }
}

public sealed class WorldSourceFactory : ISignalSourceFactory
{
    public string Kind => "world";
    public ISignalSource Create(string sourceId) => new WorldSource(sourceId);
}
