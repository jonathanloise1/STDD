using System.Text.Json.Nodes;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;

namespace MyApp.Monitor.Investigator;

public sealed record PromptParts(JsonObject Incident, JsonNode? Pack, IReadOnlySet<string>? Stories);

/// <summary>
/// What the model is told about an incident (operations.md 11.3, user turn): the trigger, severity, signature or template, counts and rate, the masked
/// triggering records as E1, a failed rule check when there is one, and, when the signature is in the pack, its record and runbook text. The shape is the one
/// the training generator writes (<c>training/investigator/scenarios.py</c>). Everything comes from the stored report, the masked triggering record and the pack.
/// </summary>
public static class IncidentPrompt
{
    /// <summary>The event kind under which an incident keeps its masked triggering record, for E1 of the prompt.</summary>
    public const string SignalEvent = "signal";

    public static PromptParts Build(JsonObject report, IReadOnlyList<JsonObject> signalRecords, OperationsPack pack)
    {
        var signature = new JsonObject();
        foreach (var kv in (JsonObject)report["signature"]!) signature[kv.Key] = kv.Value?.DeepClone();

        var records = new JsonArray();
        foreach (var r in signalRecords.Take(2)) records.Add(r.DeepClone());
        if (records.Count == 0) records.Add(FallbackRecord(report));

        var incident = new JsonObject
        {
            ["trigger"] = report["trigger"]!.DeepClone(),
            ["severity"] = report["severity"]!.DeepClone(),
            ["environment"] = report["environment"]!.DeepClone(),
            ["signature"] = signature,
            ["count"] = report["count"]!.DeepClone(),
            ["rate_per_hour"] = report["rate_per_hour"]!.DeepClone(),
            ["first_seen"] = report["first_seen"]!.DeepClone(),
            ["last_seen"] = report["last_seen"]!.DeepClone(),
            ["evidence"] = new JsonArray(new JsonObject { ["id"] = "E1", ["source"] = "signal", ["records"] = records }),
        };

        // a failed rule check is a residual condition, not a trigger (11.1): it is told next to the trigger the report has
        if (report["runbook"]?["checks"] is JsonArray checks && checks.Any(c => c?["passed"]?.GetValue<bool>() == false))
        {
            incident["rule_checks"] = new JsonArray(checks.Select(c => (JsonNode?)new JsonObject
            {
                ["tool"] = c!["tool"]!.DeepClone(), ["expect"] = c["expect"]!.DeepClone(), ["passed"] = c["passed"]!.DeepClone(),
            }).ToArray());
        }

        var id = MonitorJson.ScalarText(report["signature"]?["id"]);
        var sig = MonitorJson.ScalarText(report["signature"]?["kind"]) == "none" || id is null ? null : pack.Signatures.FirstOrDefault(s => s.Id == id);
        if (sig is null) return new PromptParts(incident, null, null);

        var node = (JsonObject)MonitorJson.ToNode(sig)!;
        node.Remove("call_site");
        if (node["match"] is JsonObject match)
            foreach (var key in match.Where(kv => kv.Value is null).Select(kv => kv.Key).ToList()) match.Remove(key);
        var stories = sig.UserStory is null ? new HashSet<string>() : new HashSet<string> { sig.UserStory };
        return new PromptParts(incident, new JsonObject { ["signature"] = node, ["runbook"] = sig.Runbook.Text }, stories);
    }

    /// <summary>An incident without the stored triggering record (ingested from another monitor): the excerpt of E1 stands for it.</summary>
    private static JsonObject FallbackRecord(JsonObject report)
    {
        var e1 = (report["evidence"] as JsonArray)?.FirstOrDefault(e => MonitorJson.ScalarText(e?["id"]) == "E1");
        var record = new JsonObject { ["Message"] = MonitorJson.ScalarText(e1?["excerpt"]) ?? "" };
        if ((report["correlation"]?["trace_ids"] as JsonArray)?.FirstOrDefault() is { } trace) record["TraceId"] = trace.DeepClone();
        record["Environment"] = report["environment"]!.DeepClone();
        return record;
    }
}

/// <summary>The triggering record in the log file format of operations.md 3.4, already masked (the pipeline masks before analysis).</summary>
public static class SignalSnapshot
{
    public static JsonObject ToJson(LogRecord r)
    {
        var o = new JsonObject
        {
            ["Timestamp"] = r.Timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture),
            ["Level"] = r.Level.ToString(),
            ["Category"] = r.Category,
        };
        if (r.LogKey is not null) o["LogKey"] = r.LogKey;
        else if (r.EventName is not null) o["EventName"] = r.EventName;
        if (r.MessageTemplate is not null) o["MessageTemplate"] = r.MessageTemplate;
        o["Message"] = r.Message;
        if (r.TraceId is not null) o["TraceId"] = r.TraceId;
        if (r.SpanId is not null) o["SpanId"] = r.SpanId;
        var props = new JsonObject();
        foreach (var kv in r.Properties) props[kv.Key] = kv.Value;
        o["Properties"] = props;
        if (r.ExceptionType is not null)
        {
            o["Exception"] = new JsonObject
            {
                ["Type"] = r.ExceptionType, ["Message"] = r.ExceptionMessage ?? "",
                ["Frames"] = r.TopFrame is null ? new JsonArray() : new JsonArray(r.TopFrame),
            };
        }
        if (r.Service is not null) o["Service"] = r.Service;
        if (r.Version is not null) o["Version"] = r.Version;
        if (r.Host is not null) o["Host"] = r.Host;
        o["Environment"] = r.Environment;
        return o;
    }
}
