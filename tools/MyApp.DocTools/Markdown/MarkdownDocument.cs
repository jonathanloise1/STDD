using System.Text.RegularExpressions;

namespace MyApp.DocTools.Markdown;

public sealed record Heading(int Level, string Text, int Line);

public sealed class MdTable
{
    public int Line { get; init; }
    public List<string> Header { get; init; } = new();
    public List<List<string>> Rows { get; init; } = new();
}

/// <summary>A small Markdown reader: headings, tables, numbered items, bullets, links. Fenced code is skipped.</summary>
public sealed class MarkdownDocument
{
    private static readonly Regex HeadingRx = new(@"^(#{1,6})\s+(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex SeparatorCell = new(@"^:?-+:?$", RegexOptions.Compiled);
    private static readonly Regex NumberedRx = new(@"^\s*\d+\.\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex BulletRx = new(@"^\s*[-*]\s+(.*)$", RegexOptions.Compiled);
    public static readonly Regex LinkRx = new(@"\[([^\]]*)\]\(([^)]*)\)", RegexOptions.Compiled);

    public string[] Lines { get; }
    public bool[] InFence { get; }
    public IReadOnlyList<Heading> Headings { get; }

    private MarkdownDocument(string[] lines, bool[] inFence, List<Heading> headings)
    {
        Lines = lines;
        InFence = inFence;
        Headings = headings;
    }

    /// <summary>Number of lines, not counting the empty remainder after a final newline.</summary>
    public int LineCount => Lines.Length > 0 && Lines[^1].Length == 0 ? Lines.Length - 1 : Lines.Length;

    public Heading? H1 => Headings.FirstOrDefault(h => h.Level == 1);

    public static MarkdownDocument Parse(string text)
    {
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var fence = new bool[lines.Length];
        var headings = new List<Heading>();
        var inFence = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith("~~~", StringComparison.Ordinal))
            {
                fence[i] = true;
                inFence = !inFence;
                continue;
            }
            fence[i] = inFence;
            if (inFence) continue;
            var m = HeadingRx.Match(lines[i]);
            if (m.Success) headings.Add(new Heading(m.Groups[1].Length, m.Groups[2].Value, i));
        }
        return new MarkdownDocument(lines, fence, headings);
    }

    public Heading? Find(int level, string text) =>
        Headings.FirstOrDefault(h => h.Level == level && h.Text == text);

    public Heading? FindStartingWith(string prefix) =>
        Headings.FirstOrDefault(h => h.Text.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>Lines after the heading up to the next heading of the same or a higher level.</summary>
    public List<string> SectionLines(Heading h)
    {
        var result = new List<string>();
        for (var i = h.Line + 1; i < Lines.Length; i++)
        {
            if (!InFence[i] && HeadingRx.IsMatch(Lines[i]) && HeadingRx.Match(Lines[i]).Groups[1].Length <= h.Level) break;
            result.Add(Lines[i]);
        }
        return result;
    }

    /// <summary>Lines of the H2 section with the given text, or null when the section is missing.</summary>
    public List<string>? Section(string h2) => Find(2, h2) is { } h ? SectionLines(h) : null;

    /// <summary>Lines between the H1 and the first H2 (the field table of stories and test cases).</summary>
    public List<string> HeadLines()
    {
        var h1 = H1;
        var first = Headings.FirstOrDefault(h => h.Level == 2);
        var start = h1 is null ? 0 : h1.Line + 1;
        var end = first?.Line ?? Lines.Length;
        return Lines.Skip(start).Take(Math.Max(0, end - start)).ToList();
    }

    public static List<MdTable> ParseTables(IEnumerable<string> lines)
    {
        var all = lines.ToList();
        var tables = new List<MdTable>();
        var i = 0;
        while (i < all.Count)
        {
            if (!all[i].TrimStart().StartsWith('|')) { i++; continue; }
            var block = new List<string>();
            var start = i;
            while (i < all.Count && all[i].TrimStart().StartsWith('|')) { block.Add(all[i]); i++; }
            if (block.Count < 2) continue;
            var sep = SplitRow(block[1]);
            if (sep.Count == 0 || !sep.All(c => SeparatorCell.IsMatch(c))) continue;
            tables.Add(new MdTable
            {
                Line = start,
                Header = SplitRow(block[0]),
                Rows = block.Skip(2).Select(SplitRow).ToList(),
            });
        }
        return tables;
    }

    public static List<string> SplitRow(string line)
    {
        var t = line.Trim();
        if (t.StartsWith('|')) t = t[1..];
        if (t.EndsWith('|') && !t.EndsWith("\\|", StringComparison.Ordinal)) t = t[..^1];
        const string placeholder = "\u0001";
        return t.Replace("\\|", placeholder).Split('|').Select(c => c.Replace(placeholder, "|").Trim()).ToList();
    }

    public static List<string> NumberedItems(IEnumerable<string> lines) =>
        lines.Select(l => NumberedRx.Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value.Trim()).ToList();

    public static List<string> Bullets(IEnumerable<string> lines) =>
        lines.Select(l => BulletRx.Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value.Trim()).ToList();

    /// <summary>True when the only non-empty line is "None."</summary>
    public static bool IsNone(IEnumerable<string> lines)
    {
        var content = lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        return content.Count == 1 && content[0] == "None.";
    }

    /// <summary>The GitHub anchor of a heading text.</summary>
    public static string Slug(string headingText)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ch in headingText.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_') sb.Append(ch);
            else if (ch == ' ') sb.Append('-');
        }
        return sb.ToString();
    }

    public static string Unquote(string cell)
    {
        var t = cell.Trim();
        return t.Length >= 2 && t[0] == '"' && t[^1] == '"' ? t[1..^1] : t;
    }
}
