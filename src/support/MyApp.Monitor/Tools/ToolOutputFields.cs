namespace MyApp.Monitor.Tools;

/// <summary>Result field paths of the working example tools (operations.md 7.4); each needs a masking class (check O08).</summary>
public static class ToolOutputFields
{
    public static readonly IReadOnlyDictionary<string, string[]> ByTool = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["logs_by_correlation"] =
        [
            "records[].timestamp", "records[].kind", "records[].level", "records[].category", "records[].log_key", "records[].template",
            "records[].message", "records[].span_id", "records[].exception_type", "records[].top_frame", "records[].name",
            "records[].result_code", "records[].duration_ms",
        ],
        ["audit_trail_by_entity"] =
        [
            "changes[].timestamp", "changes[].action", "changes[].user_id", "changes[].changed_properties[]",
            "changes[].old_values", "changes[].new_values", "changes[].correlation_id",
        ],
        ["health_status"] = ["status", "checks[].name", "checks[].status", "checks[].duration_ms"],
        ["signature_lookup"] = ["found", "signature"],
    };
}
