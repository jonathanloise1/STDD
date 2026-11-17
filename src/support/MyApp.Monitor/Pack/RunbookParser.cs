using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Rules;

namespace MyApp.Monitor.Pack;

/// <summary>One runbook section: an H2 of an area <c>operations.md</c> (5.4) or of <c>unkeyed-runbooks.md</c> (5.5).</summary>
public sealed class RunbookEntry
{
    public string Anchor { get; set; } = "";
    public int Line { get; set; }
    // keyed
    public string? LogKey { get; set; }
    public string? TestCaseId { get; set; }
    // template
    public string? EventName { get; set; }
    public string? Category { get; set; }
    public string? ExceptionType { get; set; }
    public string? TopFrame { get; set; }
    public string? Template { get; set; }
    public string? Severity { get; set; }
    // common
    public int ExpectedPerHour { get; set; }
    public List<string> FingerprintBy { get; set; } = [];
    public string CauseCategory { get; set; } = "";
    public string Status { get; set; } = "active";
    public string? FixedIn { get; set; }
    public string? NoiseRationale { get; set; }
    public string? NoiseExpires { get; set; }
    public string Text { get; set; } = "";
    /// <summary>The whole section, heading included, for <c>runbooks/{anchor}.md</c> of the pack.</summary>
    public string Markdown { get; set; } = "";
    public List<RuleCheck> RuleChecks { get; set; } = [];
    public List<HistoryEntry> History { get; set; } = [];
}

public sealed class RunbookDocument
{
    public string Path { get; init; } = "";
    public List<RunbookEntry> Entries { get; } = [];
}

/// <summary>Parses and validates runbooks. Area runbooks report check O06, unkeyed runbooks O07.</summary>
public static class RunbookParser
{
    public static readonly string[] CauseCategories =
        ["access", "data_missing", "data_conflict", "dependency_unavailable", "configuration", "capacity", "code_defect", "external_input", "expected_behaviour", "unknown"];
    public static readonly string[] Outcomes = ["fixed", "noise", "reclassified", "duplicate", "wontfix", "unknown_resolved"];
    private static readonly string[] Severities = ["Information", "Warning", "Error", "Critical"];
    private static readonly string[] SubHeadings = ["Meaning", "Check", "Rule checks", "Resolve", "History"];
    private static readonly string[] KeyedFields =
        ["Log key", "Test case", "Expected rate", "Fingerprint by", "Cause category", "Status", "Fixed in", "Noise rationale", "Noise expires"];
    private static readonly string[] TemplateFields =
        ["Event name", "Category", "Exception type", "Top frame", "Template", "Severity", "Expected rate", "Fingerprint by", "Cause category", "Status", "Fixed in", "Noise rationale", "Noise expires"];

    private static readonly Regex AnchorRegex = new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);
    private static readonly Regex TemplateIdRegex = new(@"^t-[0-9a-f]{12}$", RegexOptions.CultureInvariant);
    private static readonly Regex VersionRegex = new(@"^\d+\.\d+\.\d+([-+][0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);
    private static readonly Regex DateRegex = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant);
    private static readonly Regex RateRegex = new(@"^(\d+) per hour$", RegexOptions.CultureInvariant);
    private static readonly Regex PropertyName = new(@"^[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex HistoryRegex = new(@"^- (?<date>\d{4}-\d{2}-\d{2}) (?<inc>INC-\d{8}-\d{4}) (?<outcome>[a-z_]+)(?: (?<ref>\S.*))?$", RegexOptions.CultureInvariant);
    private static readonly Regex LinkRegex = new(@"^\[(?<text>[^\]]+)\]\((?<target>[^)]+)\)$", RegexOptions.CultureInvariant);
    private static readonly Regex ArgName = new(@"^[a-z]+(_[a-z]+)*$", RegexOptions.CultureInvariant);

    public const int MaxNoiseDays = 180;

    public sealed record Options(DateOnly Today, IReadOnlySet<string>? KnownTools = null);

    // ---- area runbooks (5.4) ----

    public static RunbookDocument ParseArea(string markdown, string relativePath, Options options, FindingList findings)
    {
        var doc = new RunbookDocument { Path = relativePath };
        var lines = Lines(markdown);
        var h1 = lines.FirstOrDefault(l => l.StartsWith("# ")) ?? "";
        if (!Regex.IsMatch(h1, @"^# .+ - Operations$")) findings.Add("O06", "H1 must be '# {Area title} - Operations'", relativePath);

        foreach (var (anchor, line, body) in SplitSections(lines))
        {
            var entry = ParseSection(anchor, line, body, isTemplate: false, relativePath, options, findings, "O06");
            if (entry is null) continue;
            if (!AnchorRegex.IsMatch(anchor)) findings.Add("O06", $"heading '{anchor}' is not a runbook anchor", Loc(relativePath, line));
            else if (entry.LogKey is not null && TestCaseParser.AnchorOf(entry.LogKey) != anchor)
                findings.Add("O06", $"heading '{anchor}' is not the anchor of log key '{entry.LogKey}'", Loc(relativePath, line));
            doc.Entries.Add(entry);
        }

        for (var i = 1; i < doc.Entries.Count; i++)
        {
            var cmp = string.CompareOrdinal(doc.Entries[i - 1].LogKey, doc.Entries[i].LogKey);
            if (cmp > 0) findings.Add("O06", $"sections must be ordered by log key: '{doc.Entries[i].LogKey}' comes after '{doc.Entries[i - 1].LogKey}'", relativePath);
            else if (cmp == 0) findings.Add("O06", $"log key '{doc.Entries[i].LogKey}' has more than one section", relativePath);
        }
        return doc;
    }

    // ---- unkeyed runbooks (5.5) ----

    public static RunbookDocument ParseUnkeyed(string markdown, string relativePath, Options options, FindingList findings)
    {
        var doc = new RunbookDocument { Path = relativePath };
        var lines = Lines(markdown);
        var h1Index = Array.FindIndex(lines, l => l.StartsWith("# "));
        if (h1Index < 0 || lines[h1Index].Trim() != "# Unkeyed signatures")
            findings.Add("O07", "H1 must be '# Unkeyed signatures'", relativePath);

        var sections = SplitSections(lines).ToList();
        var firstH2 = sections.Count > 0 ? sections[0].Line - 1 : lines.Length;
        var preamble = lines.Skip(Math.Max(h1Index + 1, 0)).Take(Math.Max(firstH2 - h1Index - 1, 0))
            .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (sections.Count == 0)
        {
            if (preamble.Count < 2 || preamble[^1] != "None.") findings.Add("O07", "needs an introductory paragraph and then 'None.' or one H2 per template id", relativePath);
        }
        else if (preamble.Count == 0) findings.Add("O07", "needs an introductory paragraph before the first H2", relativePath);

        foreach (var (anchor, line, body) in sections)
        {
            var entry = ParseSection(anchor, line, body, isTemplate: true, relativePath, options, findings, "O07");
            if (entry is null) continue;
            if (!TemplateIdRegex.IsMatch(anchor)) findings.Add("O07", $"heading '{anchor}' is not a template id", Loc(relativePath, line));
            else if (entry.Category is not null && entry.Template is not null)
            {
                var computed = TemplateExtractor.ComputeId(entry.EventName, entry.Category, entry.ExceptionType, entry.TopFrame, entry.Template);
                if (computed != anchor) findings.Add("O07", $"heading '{anchor}' must equal the template id computed from the first five fields ({computed})", Loc(relativePath, line));
            }
            doc.Entries.Add(entry);
        }
        for (var i = 1; i < doc.Entries.Count; i++)
        {
            var cmp = string.CompareOrdinal(doc.Entries[i - 1].Anchor, doc.Entries[i].Anchor);
            if (cmp > 0) findings.Add("O07", $"sections must be ordered by template id: '{doc.Entries[i].Anchor}' after '{doc.Entries[i - 1].Anchor}'", relativePath);
            else if (cmp == 0) findings.Add("O07", $"template id '{doc.Entries[i].Anchor}' has more than one section", relativePath);
        }
        return doc;
    }

    // ---- shared ----

    private static string[] Lines(string markdown) => markdown.Replace("\r\n", "\n").Split('\n');

    private static string Loc(string path, int line) => $"{path}:{line}";

    /// <summary>Splits on H2 lines outside fenced code. Returns heading text, 1-based heading line, body lines.</summary>
    private static IEnumerable<(string Heading, int Line, List<string> Body)> SplitSections(string[] lines)
    {
        var fence = false;
        string? heading = null;
        var headingLine = 0;
        var body = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            if (l.TrimStart().StartsWith("```", StringComparison.Ordinal)) fence = !fence;
            if (!fence && l.StartsWith("## ", StringComparison.Ordinal))
            {
                if (heading is not null) yield return (heading, headingLine, body);
                heading = l[3..].Trim();
                headingLine = i + 1;
                body = [];
            }
            else if (heading is not null) body.Add(l);
        }
        if (heading is not null) yield return (heading, headingLine, body);
    }

    private static RunbookEntry? ParseSection(
        string anchor, int line, List<string> body, bool isTemplate, string path, Options options, FindingList findings, string check)
    {
        var where = Loc(path, line);
        if (!MarkdownTables.TryReadTable(body, 0, out var header, out var rows, out var next))
        {
            findings.Add(check, $"section '{anchor}' must start with the field table", where);
            return null;
        }
        if (header.Count != 2 || header[0] != "Field" || header[1] != "Value")
            findings.Add(check, $"section '{anchor}': field table header must be '| Field | Value |'", where);

        var expected = isTemplate ? TemplateFields : KeyedFields;
        var labels = rows.Select(r => r.Count > 0 ? r[0] : "").ToList();
        if (!labels.SequenceEqual(expected))
        {
            findings.Add(check, $"section '{anchor}': field rows must be exactly: {string.Join(", ", expected)}", where);
            return null;
        }
        var f = rows.ToDictionary(r => r[0], r => r.Count > 1 ? r[1] : "", StringComparer.Ordinal);

        var entry = new RunbookEntry { Anchor = anchor, Line = line, Markdown = ("## " + anchor + "\n" + string.Join('\n', body)).TrimEnd() + "\n" };
        var ok = true;
        void Fail(string message) { findings.Add(check, $"section '{anchor}': {message}", where); ok = false; }
        string? NoneToNull(string v) => v == "None" ? null : v;

        if (!isTemplate)
        {
            entry.LogKey = MarkdownTables.StripBackticks(f["Log key"]);
            if (!TestCaseParser.LogKeyGrammar.IsMatch(entry.LogKey)) Fail($"log key '{entry.LogKey}' does not match the grammar");
            var link = LinkRegex.Match(f["Test case"]);
            if (!link.Success) Fail("Test case must be a link '[ID](test-cases/ID.md)'");
            else
            {
                entry.TestCaseId = link.Groups["text"].Value;
                if (link.Groups["target"].Value != $"test-cases/{entry.TestCaseId}.md") Fail($"Test case link target must be 'test-cases/{entry.TestCaseId}.md'");
            }
        }
        else
        {
            entry.EventName = NoneToNull(f["Event name"]);
            entry.Category = NoneToNull(f["Category"]);
            entry.ExceptionType = NoneToNull(f["Exception type"]);
            entry.TopFrame = NoneToNull(f["Top frame"]);
            entry.Template = NoneToNull(f["Template"]);
            entry.Severity = f["Severity"];
            if (entry.Category is null) Fail("Category is required");
            if (entry.Template is null) Fail("Template is required");
            else if (entry.Template.Length > 300) Fail("Template is longer than 300 characters");
            if (!Severities.Contains(entry.Severity)) Fail($"Severity must be one of {string.Join(", ", Severities)}");
        }

        var rate = RateRegex.Match(f["Expected rate"]);
        if (!rate.Success || !int.TryParse(rate.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var perHour)) Fail("Expected rate must be '{n} per hour' with an integer n >= 0");
        else entry.ExpectedPerHour = perHour;

        var fp = f["Fingerprint by"];
        if (fp != "None")
        {
            entry.FingerprintBy = fp.Split(',', StringSplitOptions.TrimEntries).ToList();
            if (entry.FingerprintBy.Any(n => !PropertyName.IsMatch(n))) Fail("Fingerprint by must list PascalCase property names or None");
            else if (entry.FingerprintBy.Count > 3) Fail("Fingerprint by lists at most 3 properties");
            else if (entry.FingerprintBy.Distinct().Count() != entry.FingerprintBy.Count) Fail("Fingerprint by repeats a property");
        }

        entry.CauseCategory = f["Cause category"];
        if (!CauseCategories.Contains(entry.CauseCategory)) Fail($"Cause category must be one of {string.Join(", ", CauseCategories)}");

        var statusText = f["Status"];
        switch (statusText)
        {
            case "Active": entry.Status = SignatureStatus.Active; break;
            case "Noise": entry.Status = SignatureStatus.Noise; break;
            case "Fixed": entry.Status = SignatureStatus.Fixed; break;
            default: Fail("Status must be Active, Noise or Fixed"); break;
        }

        entry.FixedIn = NoneToNull(f["Fixed in"]);
        if (entry.Status == SignatureStatus.Fixed && entry.FixedIn is null) Fail("Fixed in is required when Status is Fixed");
        if (entry.Status != SignatureStatus.Fixed && entry.FixedIn is not null) Fail("Fixed in must be None unless Status is Fixed");
        if (entry.FixedIn is not null && !VersionRegex.IsMatch(entry.FixedIn)) Fail($"Fixed in '{entry.FixedIn}' is not a version");

        entry.NoiseRationale = NoneToNull(f["Noise rationale"]);
        entry.NoiseExpires = NoneToNull(f["Noise expires"]);
        var isNoise = entry.Status == SignatureStatus.Noise;
        if (isNoise != (entry.NoiseRationale is not null) || isNoise != (entry.NoiseExpires is not null))
            Fail("Noise rationale and Noise expires are set if and only if Status is Noise");
        if (entry.NoiseRationale is { Length: > 300 }) Fail("Noise rationale is longer than 300 characters");
        if (entry.NoiseExpires is not null)
        {
            if (!DateRegex.IsMatch(entry.NoiseExpires) || !DateOnly.TryParseExact(entry.NoiseExpires, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expires))
                Fail("Noise expires must be YYYY-MM-DD");
            else
            {
                if (expires < options.Today) findings.Add("O10", $"section '{anchor}': noise expired on {entry.NoiseExpires}; renew it or change the status", where);
                if (expires > options.Today.AddDays(MaxNoiseDays)) Fail($"Noise expires more than {MaxNoiseDays} days ahead");
            }
        }

        ParseSubSections(entry, body.Skip(next).ToList(), line + next + 1, anchor, path, options, findings, check, ref ok);
        return ok ? entry : null;
    }

    private static void ParseSubSections(
        RunbookEntry entry, List<string> rest, int firstLine, string anchor, string path, Options options, FindingList findings, string check, ref bool ok)
    {
        var where = Loc(path, firstLine);
        var fence = false;
        var headings = new List<(string Name, int Index)>();
        for (var i = 0; i < rest.Count; i++)
        {
            if (rest[i].TrimStart().StartsWith("```", StringComparison.Ordinal)) fence = !fence;
            if (!fence && rest[i].StartsWith("### ", StringComparison.Ordinal)) headings.Add((rest[i][4..].Trim(), i));
        }
        if (!headings.Select(h => h.Name).SequenceEqual(SubHeadings))
        {
            findings.Add(check, $"section '{anchor}': subsections must be exactly, in order: {string.Join(", ", SubHeadings)}", where);
            ok = false;
            return;
        }

        string Content(int h)
        {
            var from = headings[h].Index + 1;
            var to = h + 1 < headings.Count ? headings[h + 1].Index : rest.Count;
            return string.Join('\n', rest.Skip(from).Take(to - from)).Trim();
        }

        foreach (var name in new[] { "Meaning", "Check", "Resolve" })
            if (Content(Array.IndexOf(SubHeadings, name)).Length == 0)
            {
                findings.Add(check, $"section '{anchor}': '{name}' is empty", where);
                ok = false;
            }

        entry.Text = string.Join('\n', rest).Trim();
        if (entry.Text.Length > 8000)
        {
            findings.Add(check, $"section '{anchor}': text below the field table is {entry.Text.Length} characters (limit 8000)", where);
            ok = false;
        }

        ParseRuleChecks(entry, Content(2), anchor, path, firstLine, options, findings, check, ref ok);
        ParseHistory(entry, Content(4), anchor, path, firstLine, findings, check, ref ok);
    }

    private static void ParseRuleChecks(
        RunbookEntry entry, string content, string anchor, string path, int line, Options options, FindingList findings, string check, ref bool ok)
    {
        var where = Loc(path, line);
        if (content == "None.") return;
        var lines = content.Split('\n');
        if (!MarkdownTables.TryReadTable(lines, 0, out var header, out var rows, out _)
            || header.Count != 3 || header[0] != "Tool" || header[1] != "Arguments" || header[2] != "Expect")
        {
            findings.Add(check, $"section '{anchor}': Rule checks must be 'None.' or a table '| Tool | Arguments | Expect |'", where);
            ok = false;
            return;
        }
        if (rows.Count > 10) { findings.Add(check, $"section '{anchor}': at most 10 rule checks", where); ok = false; }
        foreach (var row in rows)
        {
            if (row.Count != 3) { findings.Add(check, $"section '{anchor}': a rule check row needs three cells", where); ok = false; continue; }
            var tool = MarkdownTables.StripBackticks(row[0]);
            if (!ArgName.IsMatch(tool)) { findings.Add(check, $"section '{anchor}': '{tool}' is not a tool name", where); ok = false; continue; }
            if (options.KnownTools is not null && !options.KnownTools.Contains(tool))
            {
                findings.Add(check, $"section '{anchor}': rule check names unknown tool '{tool}'", where);
                ok = false;
                continue;
            }
            var arguments = new JsonObject();
            var argText = row[1].Trim();
            if (argText != "-")
            {
                foreach (var pair in argText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    var eq = pair.IndexOf('=');
                    if (eq <= 0 || !ArgName.IsMatch(pair[..eq].Trim())) { findings.Add(check, $"section '{anchor}': argument '{pair}' must be name=value", where); ok = false; continue; }
                    arguments[pair[..eq].Trim()] = Literal(pair[(eq + 1)..].Trim());
                }
            }
            var expect = MarkdownTables.StripBackticks(row[2]);
            if (!Expectation.TryParse(expect, out _, out var error)) { findings.Add(check, $"section '{anchor}': {error}", where); ok = false; continue; }
            entry.RuleChecks.Add(new RuleCheck { Tool = tool, Arguments = arguments, Expect = expect });
        }
    }

    private static JsonNode Literal(string text)
    {
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"') return JsonValue.Create(text[1..^1])!;
        if (text is "true" or "false") return JsonValue.Create(text == "true");
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return JsonValue.Create(l);
        return JsonValue.Create(text)!;
    }

    private static void ParseHistory(RunbookEntry entry, string content, string anchor, string path, int line, FindingList findings, string check, ref bool ok)
    {
        var where = Loc(path, line);
        if (content == "None.") return;
        foreach (var raw in content.Split('\n'))
        {
            var l = raw.TrimEnd();
            if (l.Length == 0) continue;
            var m = HistoryRegex.Match(l);
            if (!m.Success || !Outcomes.Contains(m.Groups["outcome"].Value))
            {
                findings.Add(check, $"section '{anchor}': History line '{l}' must be '- {{YYYY-MM-DD}} {{incident id}} {{outcome}} {{link}}'", where);
                ok = false;
                continue;
            }
            entry.History.Add(new HistoryEntry
            {
                Date = m.Groups["date"].Value,
                Incident = m.Groups["inc"].Value,
                Outcome = m.Groups["outcome"].Value,
                Reference = m.Groups["ref"].Success ? m.Groups["ref"].Value : null,
            });
        }
        if (entry.History.Count > 100) { findings.Add(check, $"section '{anchor}': at most 100 History lines", where); ok = false; }
    }
}
