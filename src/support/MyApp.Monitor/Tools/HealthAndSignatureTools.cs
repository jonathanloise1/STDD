using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyApp.Monitor.Tools;

internal static class HealthSupport
{
    public static ToolResult Map(JsonNode? body)
    {
        if (body is not JsonObject obj) throw new ToolException("the health endpoint did not return a JSON object");
        var checks = new JsonArray();
        foreach (var c in (obj["checks"] as JsonArray) ?? [])
        {
            checks.Add(new JsonObject
            {
                ["name"] = MonitorJson.ScalarText(MonitorJson.Get(c, "name")),
                ["status"] = MonitorJson.ScalarText(MonitorJson.Get(c, "status")),
                ["duration_ms"] = MonitorJson.Get(c, "durationMs")?.DeepClone() ?? MonitorJson.Get(c, "duration_ms")?.DeepClone(),
            });
        }
        var data = new JsonObject { ["status"] = MonitorJson.ScalarText(obj["status"]), ["checks"] = checks };
        return new ToolResult(data, Math.Max(1, checks.Count));
    }
}

/// <summary><c>health_status</c> over HTTP: the backend's <c>/health</c> (a 503 with a JSON body is an answer, not a failure).</summary>
public sealed class HealthStatusHttp(ToolServices services) : IToolImplementation
{
    public string Family => "health";
    public string SourceKind => "http_health";

    public async Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var factory = services.Http ?? throw new ToolException("no HTTP client factory is configured");
        var baseUrl = ctx.Source.GetString("base_url") ?? throw new ToolException("the source has no base_url");
        var path = MonitorJson.ScalarText(ctx.Settings["path"]) ?? "/health";
        var client = factory.CreateClient("monitor-health");
        using var response = await client.GetAsync(baseUrl.TrimEnd('/') + path, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        JsonNode? body;
        try { body = JsonNode.Parse(text); }
        catch (JsonException) { throw new ToolException($"the health endpoint answered {(int)response.StatusCode} without JSON"); }
        return HealthSupport.Map(body);
    }
}

/// <summary><c>health_status</c> on a world directory: <c>health.json</c>.</summary>
public sealed class HealthStatusWorld : IToolImplementation
{
    public string Family => "health";
    public string SourceKind => "world";

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var directory = ctx.Source.GetString("directory") ?? throw new ToolException("the world source has no directory");
        var file = Path.Combine(directory, MonitorJson.ScalarText(ctx.Settings["file"]) ?? "health.json");
        if (!File.Exists(file)) throw new ToolException("the world has no health.json");
        return Task.FromResult(HealthSupport.Map(MonitorJson.Parse(File.ReadAllText(file))));
    }
}

/// <summary><c>signature_lookup</c> on the operations pack: exactly one of <c>signature_id</c>, <c>log_key</c>, <c>template_id</c>.</summary>
public sealed class SignatureLookupPack(ToolServices services) : IToolImplementation
{
    public string Family => "signatures";
    public string SourceKind => "pack";

    public Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var pack = services.Pack?.Current ?? throw new ToolException("no operations pack is loaded");
        var given = new[] { "signature_id", "log_key", "template_id" }.Where(n => ctx.Arguments[n] is not null).ToList();
        if (given.Count != 1) throw new ToolArgumentException("give exactly one of signature_id, log_key, template_id");
        var value = ToolValues.Required(ctx.Arguments, given[0]);

        var signature = given[0] switch
        {
            "log_key" => pack.FindByLogKey(value),
            "template_id" => pack.FindByTemplateId(value),
            _ => pack.Signatures.FirstOrDefault(s => s.Id == value),
        };
        JsonNode? node = null;
        if (signature is not null)
        {
            node = MonitorJson.ToNode(signature);
            foreach (var path in (ctx.Settings["exclude_fields"] as JsonArray)?.Select(n => MonitorJson.ScalarText(n) ?? "") ?? [])
                Remove(node, path);
        }
        return Task.FromResult(new ToolResult(new JsonObject { ["found"] = signature is not null, ["signature"] = node }, signature is null ? 0 : 1));
    }

    private static void Remove(JsonNode? root, string path)
    {
        var segments = path.Split('.');
        var node = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (!MonitorJson.TryGet(node, segments[i], out node) || node is null) return;
        }
        if (node is JsonObject obj)
            foreach (var key in obj.Select(kv => kv.Key).ToList())
                if (string.Equals(key, segments[^1], StringComparison.OrdinalIgnoreCase)) obj.Remove(key);
    }
}
