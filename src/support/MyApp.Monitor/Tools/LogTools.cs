using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyApp.Monitor.Sources;

namespace MyApp.Monitor.Tools;

internal static class LogsByCorrelationSupport
{
    private static readonly Regex TraceId = new(@"^[0-9a-f]{32}$", RegexOptions.CultureInvariant);

    public static (string TraceId, int Limit) Arguments(ToolContext ctx)
    {
        var trace = ToolValues.Required(ctx.Arguments, "trace_id");
        if (!TraceId.IsMatch(trace)) throw new ToolArgumentException("trace_id must be 32 lower-case hex characters");
        return (trace, ToolValues.Limit(ctx.Arguments, "limit", 100, Math.Min(200, ctx.Limits.MaxRows)));
    }

    public static ToolResult Result(IReadOnlyList<JsonObject> records) =>
        new(new JsonObject { ["records"] = new JsonArray(records.Select(r => (JsonNode?)r).ToArray()) }, records.Count);
}

/// <summary><c>logs_by_correlation</c> on Log Analytics: the catalog's query text with validated values only.</summary>
public sealed class LogsByCorrelationLogAnalytics(ToolServices services) : IToolImplementation
{
    public string Family => "logs";
    public string SourceKind => "log_analytics";

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var client = services.LogAnalytics ?? throw new ToolException("no Log Analytics client is configured");
        var resolver = services.Workspaces ?? throw new ToolException("no workspace resolver is configured");
        var (trace, limit) = LogsByCorrelationSupport.Arguments(ctx);
        var template = MonitorJson.ScalarText(ctx.Settings["query"]) ?? throw new ToolException("the catalog has no query for log_analytics");
        var lookback = ctx.Limits.LookbackHours ?? 72;
        var query = template
            .Replace("{lookback_hours}", lookback.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{trace_id}", trace, StringComparison.Ordinal)
            .Replace("{limit}", limit.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        if (Regex.IsMatch(query, @"\{[a-z_]+\}")) throw new ToolException("the query has a placeholder that is not a parameter or a limit");

        var workspace = await resolver.ResolveAsync(ctx.Source.Id, ctx.Source.Settings, ct).ConfigureAwait(false);
        var rows = await client.QueryAsync(workspace, query, ct).ConfigureAwait(false);
        var columns = ctx.Settings["columns"] as JsonObject ?? throw new ToolException("the catalog has no columns for log_analytics");

        var records = new List<JsonObject>();
        foreach (var row in rows.Take(limit))
        {
            var json = ToolValues.Row(row);
            var record = JsonColumns.Map(json, columns);
            record["kind"] = KindOf(MonitorJson.ScalarText(json["Type"]));
            if (record["level"] is JsonValue lv && int.TryParse(MonitorJson.ScalarText(lv), NumberStyles.Integer, CultureInfo.InvariantCulture, out var severity))
                record["level"] = LogRecord.FromSeverityLevel(severity).ToString();
            records.Add(record);
        }
        return LogsByCorrelationSupport.Result(records);
    }

    private static string KindOf(string? type) => type switch
    {
        "AppExceptions" => "exception",
        "AppRequests" => "request",
        "AppDependencies" => "dependency",
        _ => "trace",
    };
}

/// <summary><c>logs_by_correlation</c> on the bound log files, newest file first.</summary>
public sealed class LogsByCorrelationLogFiles(ToolServices services) : IToolImplementation
{
    public string Family => "logs";
    public string SourceKind => "log_files";

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var (trace, limit) = LogsByCorrelationSupport.Arguments(ctx);
        var patterns = ctx.Source.GetStrings("paths");
        if (patterns.Count == 0) throw new ToolException("the source has no paths");
        var matchField = MonitorJson.ScalarText(ctx.Settings["match_field"]) ?? "TraceId";
        var columns = ctx.Settings["columns"] as JsonObject ?? JsonlTraceSearch.DefaultColumns;
        var since = ctx.Limits.LookbackHours is { } h ? services.Time.GetUtcNow().AddHours(-h) : (DateTimeOffset?)null;

        var files = GlobExpander.Expand(patterns, services.ContentRoot).OrderByDescending(f => f.LastWriteTimeUtc).ThenBy(f => f.FullName, StringComparer.Ordinal);
        var records = JsonlTraceSearch.Search(files, trace, matchField, columns, limit, since);
        return Task.FromResult(LogsByCorrelationSupport.Result(records));
    }
}

/// <summary><c>logs_by_correlation</c> on a simulated world directory (<c>logs.jsonl</c>).</summary>
public sealed class LogsByCorrelationWorld(ToolServices services) : IToolImplementation
{
    public string Family => "logs";
    public string SourceKind => "world";

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var (trace, limit) = LogsByCorrelationSupport.Arguments(ctx);
        var directory = ctx.Source.GetString("directory") ?? throw new ToolException("the world source has no directory");
        var file = Path.Combine(directory, MonitorJson.ScalarText(ctx.Settings["file"]) ?? "logs.jsonl");
        if (!File.Exists(file)) return Task.FromResult(LogsByCorrelationSupport.Result([]));
        var matchField = MonitorJson.ScalarText(ctx.Settings["match_field"]) ?? "TraceId";
        var records = JsonlTraceSearch.Search([new FileInfo(file)], trace, matchField, JsonlTraceSearch.DefaultColumns, limit, null);
        _ = services;
        return Task.FromResult(LogsByCorrelationSupport.Result(records));
    }
}
