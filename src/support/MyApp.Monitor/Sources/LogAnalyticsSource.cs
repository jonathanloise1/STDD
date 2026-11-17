using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Sources;

public sealed record WatermarkCheckpoint(string Watermark);

/// <summary>Resolves the workspace of a <c>log_analytics</c> source: configured, or discovered by tag and cached for 6 hours (operations.md 6.3).</summary>
public sealed class WorkspaceResolver(IWorkspaceDiscoveryClient? discovery, ISourceStateStore state, MonitorOptions options, TimeProvider time) : IWorkspaceResolver
{
    public static readonly TimeSpan RefreshEvery = TimeSpan.FromHours(6);
    public const string DefaultResourceType = "Microsoft.OperationalInsights/workspaces";

    public async Task<string> ResolveAsync(string sourceId, JsonObject sourceSettings, CancellationToken ct)
    {
        var explicitId = MonitorJson.ScalarText(sourceSettings["workspace_id"]);
        if (!string.IsNullOrWhiteSpace(explicitId)) return explicitId;

        var d = sourceSettings["discovery"] as JsonObject
            ?? throw new SourceConfigurationException($"source '{sourceId}': give workspace_id or a discovery tag");
        var tagName = MonitorJson.ScalarText(d["tag_name"]) ?? "";
        var tagValue = MonitorJson.ScalarText(d["tag_value"]) ?? "";
        var type = MonitorJson.ScalarText(d["resource_type"]) ?? DefaultResourceType;
        if (tagName.Length == 0 || tagValue.Length == 0) throw new SourceConfigurationException($"source '{sourceId}': discovery needs tag_name and tag_value");

        var now = time.GetUtcNow();
        var cached = await state.GetDiscoveredAsync(sourceId, ct).ConfigureAwait(false);
        if (cached is not null && now - cached.RefreshedAt < RefreshEvery) return cached.ResourceId;
        if (discovery is null) throw new SourceConfigurationException($"source '{sourceId}': no discovery client is configured");

        IReadOnlyList<string> found;
        try { found = await discovery.FindWorkspacesAsync(tagName, tagValue, type, options.Azure.SubscriptionIdList, ct).ConfigureAwait(false); }
        catch (Exception ex) when (cached is not null && ex is not OperationCanceledException)
        {
            return cached.ResourceId;   // keep working on the cached workspace when a refresh fails
        }
        if (found.Count != 1)
            throw new SourceConfigurationException(
                $"source '{sourceId}': discovery by tag '{tagName}={tagValue}' found {found.Count} workspaces of type {type}; exactly one is required");
        await state.SetDiscoveredAsync(new DiscoveredResource(sourceId, found[0], now), ct).ConfigureAwait(false);
        return found[0];
    }
}

/// <summary>The <c>log_analytics</c> source (operations.md 6.3): polls AppTraces and AppExceptions on <c>ingestion_time()</c>.</summary>
public sealed class LogAnalyticsSource(string id, ILogAnalyticsClient client, IWorkspaceResolver resolver) : PollingSourceBase(id, "log_analytics")
{
    private static readonly Regex RoleName = new(@"^[A-Za-z0-9_.\- ]+$", RegexOptions.CultureInvariant);

    private IReadOnlyList<string> _roles = [];
    private int _minLevel = 2;
    private int _maxRows = 5000;
    private int _lookbackMinutes = 60;
    private bool _continue;
    private string? _workspace;

    protected override TimeSpan PollInterval => TimeSpan.FromSeconds(Math.Max(1, Context.GetInt("poll_seconds", 60)));
    protected override bool ContinueImmediately => _continue;

    protected override Task OnStartAsync(CancellationToken ct)
    {
        _roles = Context.GetStrings("app_role_names");
        if (_roles.Count == 0) throw new SourceConfigurationException($"source '{Id}': app_role_names must list at least one role");
        foreach (var r in _roles)
            if (!RoleName.IsMatch(r)) throw new SourceConfigurationException($"source '{Id}': app role name '{r}' has characters that are not allowed");
        _minLevel = Context.GetInt("min_level", 2);
        _maxRows = Math.Max(1, Context.GetInt("max_rows_per_poll", 5000));
        _lookbackMinutes = Math.Max(1, Context.GetInt("lookback_minutes_first_run", 60));
        return Task.CompletedTask;
    }

    protected override IReadOnlyDictionary<string, string> HealthDetails() => new Dictionary<string, string>
    {
        ["workspace"] = _workspace ?? "",
    };

    public override async Task CommitAsync(SignalBatch batch, CancellationToken ct)
    {
        if (batch.Checkpoint is WatermarkCheckpoint cp) await Context.State.SetWatermarkAsync(Id, cp.Watermark, ct).ConfigureAwait(false);
    }

    public string BuildQuery(DateTimeOffset watermark, DateTimeOffset now) => $"""
        union AppTraces, AppExceptions
        | where ingestion_time() > datetime({Iso(watermark)}) and ingestion_time() <= datetime({Iso(now)})
        | where AppRoleName in ({string.Join(", ", _roles.Select(r => $"'{r}'"))})
        | where SeverityLevel >= {_minLevel.ToString(CultureInfo.InvariantCulture)} or isnotempty(tostring(Properties.LogKey))
        | extend IngestedAt = ingestion_time()
        | project IngestedAt, TimeGenerated, Type, SeverityLevel, Message, ExceptionType, OuterMessage, Method,
                  OperationId, ParentId, AppVersion, AppRoleInstance, Properties
        | order by IngestedAt asc
        | take {_maxRows.ToString(CultureInfo.InvariantCulture)}
        """;

    private static string Iso(DateTimeOffset t) => t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    protected override async Task<IReadOnlyList<SignalBatch>> PollAsync(CancellationToken ct)
    {
        _continue = false;
        _workspace = await resolver.ResolveAsync(Id, Context.Settings, ct).ConfigureAwait(false);
        var now = Time.GetUtcNow();
        var saved = await Context.State.GetWatermarkAsync(Id, ct).ConfigureAwait(false);
        var watermark = saved is not null && DateTimeOffset.TryParse(saved, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var w)
            ? w : now.AddMinutes(-_lookbackMinutes);

        var rows = await client.QueryAsync(_workspace, BuildQuery(watermark, now), ct).ConfigureAwait(false);
        if (rows.Count == 0) return [];

        var mapped = rows
            .Select(r => (Ingested: IngestedAt(r), Record: LogAnalyticsRowMapper.Map(r, Id, Context.Environment)))
            .OrderBy(x => x.Ingested).ToList();
        var maxIngested = mapped.Max(x => x.Ingested);
        _continue = rows.Count >= _maxRows;
        return [new SignalBatch(Id, mapped.Select(x => x.Record).ToList(), new WatermarkCheckpoint(Iso(maxIngested)))];
    }

    private static DateTimeOffset IngestedAt(IReadOnlyDictionary<string, object?> row) =>
        LogAnalyticsRowMapper.Time(row, "IngestedAt") ?? LogAnalyticsRowMapper.Time(row, "TimeGenerated") ?? DateTimeOffset.UtcNow;
}

/// <summary>Maps one row of the poll query to the normal form (operations.md 6.3 Mapping).</summary>
public static class LogAnalyticsRowMapper
{
    public static LogRecord Map(IReadOnlyDictionary<string, object?> row, string sourceId, string environment)
    {
        var props = Properties(row);
        string? Text(string name) => row.TryGetValue(name, out var v) ? MonitorJson.ScalarText(ToolValues.FromObject(v)) : null;

        var type = Text("Type");
        var message = Text("Message");
        var outer = Text("OuterMessage");
        var isException = type == "AppExceptions";
        var level = int.TryParse(Text("SeverityLevel"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sev) ? LogRecord.FromSeverityLevel(sev) : RecordLevel.Information;

        return LogRecord.Create(
            sourceId, environment, Time(row, "TimeGenerated") ?? Time(row, "IngestedAt") ?? DateTimeOffset.UtcNow, level,
            props.GetValueOrDefault("CategoryName"),
            string.IsNullOrEmpty(message) ? outer ?? "" : message,
            eventName: props.GetValueOrDefault("LogKey"),
            messageTemplate: props.GetValueOrDefault("{OriginalFormat}"),
            traceId: Text("OperationId"),
            spanId: Text("ParentId"),
            properties: props,
            exceptionType: props.GetValueOrDefault("ExceptionType") ?? Text("ExceptionType"),
            exceptionMessage: isException ? outer : null,
            topFrame: props.GetValueOrDefault("TopFrame") ?? Text("Method"),
            version: Text("AppVersion"),
            host: Text("AppRoleInstance"));
    }

    public static DateTimeOffset? Time(IReadOnlyDictionary<string, object?> row, string name)
    {
        if (!row.TryGetValue(name, out var v) || v is null) return null;
        if (v is DateTimeOffset dto) return dto.ToUniversalTime();
        if (v is DateTime dt) return new DateTimeOffset(dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt).ToUniversalTime();
        return DateTimeOffset.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t) ? t : null;
    }

    private static Dictionary<string, string> Properties(IReadOnlyDictionary<string, object?> row)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!row.TryGetValue("Properties", out var raw) || raw is null) return result;
        var node = ToolValues.FromObject(raw);
        if (node is JsonValue v && v.TryGetValue<string>(out var text))
        {
            try { node = JsonNode.Parse(text); } catch (System.Text.Json.JsonException) { return result; }
        }
        if (node is JsonObject obj)
            foreach (var kv in obj)
                result[kv.Key] = kv.Value is JsonValue ? MonitorJson.ScalarText(kv.Value) ?? "" : MonitorJson.ToCompact(kv.Value);
        return result;
    }
}

public sealed class LogAnalyticsSourceFactory(ILogAnalyticsClient client, IWorkspaceResolver resolver) : ISignalSourceFactory
{
    public string Kind => "log_analytics";
    public ISignalSource Create(string sourceId) => new LogAnalyticsSource(sourceId, client, resolver);
}
