using System.Text.RegularExpressions;

namespace MyApp.Monitor.Pack;

public sealed record ObservableTrace(string? Message, string LogKey, string Severity, string RunbookLinkText, string RunbookLinkTarget);

public sealed record TestCase(
    string Id, string Area, string Path, string UserStory, string Type, string Title, ObservableTrace? Trace);

/// <summary>Parses test case files (development.md section 8) for the Observable trace block (8.5).</summary>
public static class TestCaseParser
{
    public static readonly string[] SeverityValues = ["Information", "Warning", "Error", "Critical"];
    public static readonly Regex LogKeyGrammar = new(@"^[a-z]{2,8}\.[a-z]{2,12}\.[a-z]+(_[a-z]+)*$", RegexOptions.CultureInvariant);
    private static readonly Regex TitleRegex = new(@"^#\s+([A-Z]{2,8}-[A-Z]{2,12}-\d{3}):\s*(.+)$", RegexOptions.CultureInvariant);
    private static readonly Regex LinkRegex = new(@"^\[(?<text>[^\]]+)\]\((?<target>[^)]+)\)$", RegexOptions.CultureInvariant);

    public static string AnchorOf(string logKey) => logKey.Replace('.', '-').Replace('_', '-');

    /// <summary>Reads every test case of <c>docs/areas/*/test-cases/*.md</c>. Problems are added as R13 findings.</summary>
    public static List<TestCase> ReadAll(string repoRoot, FindingList findings)
    {
        var result = new List<TestCase>();
        var areasDir = System.IO.Path.Combine(repoRoot, "docs", "areas");
        if (!Directory.Exists(areasDir)) { findings.Add("R13", "docs/areas not found", areasDir); return result; }
        foreach (var areaDir in Directory.GetDirectories(areasDir).OrderBy(d => d, StringComparer.Ordinal))
        {
            var tcDir = System.IO.Path.Combine(areaDir, "test-cases");
            if (!Directory.Exists(tcDir)) continue;
            var area = System.IO.Path.GetFileName(areaDir);
            foreach (var file in Directory.GetFiles(tcDir, "*.md").OrderBy(f => f, StringComparer.Ordinal))
            {
                var rel = $"docs/areas/{area}/test-cases/{System.IO.Path.GetFileName(file)}";
                var tc = Parse(File.ReadAllText(file), area, rel, findings);
                if (tc is not null) result.Add(tc);
            }
        }
        return result;
    }

    public static TestCase? Parse(string markdown, string area, string relativePath, FindingList findings)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var first = lines.FirstOrDefault(l => l.StartsWith("# ")) ?? "";
        var title = TitleRegex.Match(first);
        if (!title.Success) { findings.Add("R13", "test case heading must be '# {ID}: {Title}'", relativePath); return null; }
        var id = title.Groups[1].Value;

        var fields = ReadFieldTable(lines, Array.IndexOf(lines, first) + 1, out _);
        fields.TryGetValue("User Story", out var story);
        fields.TryGetValue("Type", out var type);
        if (string.IsNullOrEmpty(story) || string.IsNullOrEmpty(type))
        {
            findings.Add("R13", "field table needs User Story and Type", relativePath);
            return null;
        }

        var obsIndex = Array.FindIndex(lines, l => l.TrimEnd() == "## Observable trace");
        var needsTrace = type is "Edge case" or "Error handling";
        ObservableTrace? trace = null;
        if (needsTrace && obsIndex < 0) findings.Add("R13", $"Type '{type}' requires an Observable trace", relativePath);
        else if (!needsTrace && obsIndex >= 0) findings.Add("R13", $"Type '{type}' must not have an Observable trace", relativePath);
        else if (obsIndex >= 0) trace = ParseTrace(lines, obsIndex, relativePath, findings);

        return new TestCase(id, area, relativePath, story, type, title.Groups[2].Value.Trim(), trace);
    }

    private static Dictionary<string, string> ReadFieldTable(string[] lines, int start, out int next)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        next = start;
        if (!MarkdownTables.TryReadTable(lines, start, out _, out var rows, out next)) return fields;
        foreach (var row in rows)
            if (row.Count >= 2) fields[row[0]] = row[1];
        return fields;
    }

    private static ObservableTrace? ParseTrace(string[] lines, int heading, string path, FindingList findings)
    {
        var fields = ReadFieldTable(lines, heading + 1, out _);
        string? Need(string name)
        {
            if (fields.TryGetValue(name, out var v) && v.Length > 0) return v;
            findings.Add("R13", $"Observable trace is missing '{name}'", path);
            return null;
        }

        var message = Need("Message");
        var key = Need("Log key");
        var severity = Need("Severity");
        var runbook = Need("Runbook");
        if (message is null || key is null || severity is null || runbook is null) return null;

        key = MarkdownTables.StripBackticks(key);
        if (!LogKeyGrammar.IsMatch(key)) { findings.Add("R13", $"log key '{key}' does not match the grammar", path); return null; }
        if (!SeverityValues.Contains(severity)) { findings.Add("R13", $"severity '{severity}' is not one of {string.Join(", ", SeverityValues)}", path); return null; }
        var link = LinkRegex.Match(runbook);
        if (!link.Success) { findings.Add("R13", "Runbook must be a link '[anchor](../operations.md#anchor)'", path); return null; }

        var msg = message.Trim();
        string? messageValue = msg == "None" ? null : MarkdownTables.StripQuotes(msg);
        return new ObservableTrace(messageValue, key, severity, link.Groups["text"].Value, link.Groups["target"].Value);
    }
}
