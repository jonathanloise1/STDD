using System.Text;

namespace MyApp.Monitor.Pack;

/// <summary>Small Markdown helpers for the formats of development.md and operations.md (tables, fenced blocks).</summary>
public static class MarkdownTables
{
    public static bool IsTableLine(string line) => line.TrimStart().StartsWith('|');

    public static bool IsSeparator(IReadOnlyList<string> cells) =>
        cells.Count > 0 && cells.All(c => c.Length > 0 && c.All(ch => ch is '-' or ':' or ' '));

    /// <summary>Splits a table row into trimmed cells; <c>\|</c> inside a cell is an escaped pipe.</summary>
    public static List<string> SplitRow(string line)
    {
        var text = line.Trim();
        if (text.StartsWith('|')) text = text[1..];
        if (text.EndsWith('|') && !text.EndsWith("\\|", StringComparison.Ordinal)) text = text[..^1];
        var cells = new List<string>();
        var sb = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == '|') { sb.Append('|'); i++; }
            else if (text[i] == '|') { cells.Add(sb.ToString().Trim()); sb.Clear(); }
            else sb.Append(text[i]);
        }
        cells.Add(sb.ToString().Trim());
        return cells;
    }

    /// <summary>
    /// Reads the first table found at or after <paramref name="start"/> (skipping blank lines only).
    /// Returns the data rows (header and separator removed), the header, and the index after the table.
    /// </summary>
    public static bool TryReadTable(IReadOnlyList<string> lines, int start, out List<string> header, out List<List<string>> rows, out int next)
    {
        header = [];
        rows = [];
        next = start;
        var i = start;
        while (i < lines.Count && lines[i].Trim().Length == 0) i++;
        if (i >= lines.Count || !IsTableLine(lines[i])) return false;
        header = SplitRow(lines[i]);
        i++;
        if (i < lines.Count && IsTableLine(lines[i]) && IsSeparator(SplitRow(lines[i]))) i++;
        while (i < lines.Count && IsTableLine(lines[i]))
        {
            rows.Add(SplitRow(lines[i]));
            i++;
        }
        next = i;
        return true;
    }

    public static string StripBackticks(string value)
    {
        var v = value.Trim();
        return v.Length >= 2 && v[0] == '`' && v[^1] == '`' ? v[1..^1] : v;
    }

    public static string StripQuotes(string value)
    {
        var v = value.Trim();
        if (v.Length >= 2 && ((v[0] == '"' && v[^1] == '"') || (v[0] == '“' && v[^1] == '”'))) return v[1..^1];
        return v;
    }
}
