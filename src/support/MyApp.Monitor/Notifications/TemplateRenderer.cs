using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MyApp.Monitor.Notifications;

public sealed record RenderResult(string Text, IReadOnlyList<string> UnknownPaths);

/// <summary>
/// Notification template renderer (operations.md 10.5). <c>{{path}}</c> inserts a report field (dot path, arrays by index);
/// <c>{{json:path}}</c> inserts it JSON-escaped; <c>{{evidence_lines}}</c> one line per evidence item;
/// <c>{{dashboard_url}}</c> the incident page. A null value renders empty; an unknown path renders empty and is reported.
/// </summary>
public static class TemplateRenderer
{
    private static readonly Regex Tag = new(@"\{\{\s*(?<json>json:)?(?<path>[A-Za-z_][A-Za-z0-9_.\[\]]*)\s*\}\}", RegexOptions.CultureInvariant);

    public static RenderResult Render(string template, JsonNode report, string dashboardUrl, bool includeExcerpts = true)
    {
        var unknown = new List<string>();
        var text = Tag.Replace(template, m =>
        {
            var path = m.Groups["path"].Value;
            string value;
            if (path == "evidence_lines") value = EvidenceLines(report, includeExcerpts);
            else if (path == "dashboard_url") value = dashboardUrl;
            else if (MonitorJson.TryGet(report, path, out var node)) value = Text(node);
            else { unknown.Add(path); value = ""; }
            return m.Groups["json"].Success ? JsonEncodedText.Encode(value, System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString() : value;
        });
        return new RenderResult(text, unknown.Distinct().ToList());
    }

    private static string Text(JsonNode? node) => node switch
    {
        null => "",
        JsonValue => MonitorJson.ScalarText(node) ?? "",
        _ => MonitorJson.ToCompact(node),
    };

    private static string EvidenceLines(JsonNode report, bool includeExcerpts)
    {
        var sb = new StringBuilder();
        foreach (var e in (report["evidence"] as JsonArray) ?? [])
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"{Text(e?["id"])} {Text(e?["source"])} {Text(e?["reference"])}");
            if (includeExcerpts) sb.Append($": {Text(e?["excerpt"])}");
        }
        return sb.ToString();
    }
}
