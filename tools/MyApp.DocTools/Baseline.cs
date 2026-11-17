using System.Text;
using System.Text.Json;

namespace MyApp.DocTools;

public sealed record BaselineEntry(string Rule, string Subject);

public sealed record BaselineResult(List<Finding> New, List<Finding> Known, List<BaselineEntry> Stale);

/// <summary>The baseline of development.md section 12.3.</summary>
public sealed class Baseline
{
    public List<BaselineEntry> Entries { get; init; } = new();

    public static Baseline Empty => new();

    public static Baseline Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Baseline file not found: {path}");
        return Parse(File.ReadAllText(path));
    }

    public static Baseline Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var list = new List<BaselineEntry>();
        if (doc.RootElement.TryGetProperty("findings", out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var e in arr.EnumerateArray())
                list.Add(new BaselineEntry(e.GetProperty("rule").GetString() ?? "", e.GetProperty("subject").GetString() ?? ""));
        return new Baseline { Entries = list };
    }

    /// <summary>Entries must be sorted by rule then subject and unique.</summary>
    public List<string> Problems()
    {
        var problems = new List<string>();
        var sorted = Entries.OrderBy(e => e.Rule, StringComparer.Ordinal).ThenBy(e => e.Subject, StringComparer.Ordinal).ToList();
        if (!Entries.SequenceEqual(sorted.Distinct())) problems.Add("The baseline entries are not sorted by rule, then subject, or contain duplicates.");
        return problems;
    }

    public static BaselineResult Compare(IEnumerable<Finding> findings, Baseline baseline)
    {
        var listed = baseline.Entries.Select(e => (e.Rule, e.Subject)).ToHashSet();
        var all = findings.ToList();
        var known = all.Where(f => listed.Contains((f.Rule, f.Subject))).ToList();
        var fresh = all.Where(f => !listed.Contains((f.Rule, f.Subject))).ToList();
        var present = all.Select(f => (f.Rule, f.Subject)).ToHashSet();
        var stale = baseline.Entries.Where(e => !present.Contains((e.Rule, e.Subject))).Distinct().ToList();
        return new BaselineResult(fresh, known, stale);
    }

    /// <summary>
    /// Entries of the current baseline that the target branch does not have, for rules the target branch already
    /// has in section 12.2. A rule the target does not know yet is being introduced by this change, so its entries pass.
    /// </summary>
    public static List<BaselineEntry> AddedSince(Baseline current, Baseline target, IReadOnlySet<string> targetRules) =>
        current.Entries.Where(e => !target.Entries.Contains(e) && targetRules.Contains(e.Rule)).Distinct().ToList();

    public static string Serialize(IEnumerable<Finding> findings)
    {
        var entries = findings.Select(f => (f.Rule, f.Subject)).Distinct()
            .OrderBy(e => e.Rule, StringComparer.Ordinal).ThenBy(e => e.Subject, StringComparer.Ordinal).ToList();
        if (entries.Count == 0) return "{ \"findings\": [] }\n";
        var sb = new StringBuilder("{\n  \"findings\": [\n");
        for (var i = 0; i < entries.Count; i++)
            sb.Append($"    {{ \"rule\": {JsonSerializer.Serialize(entries[i].Rule)}, \"subject\": {JsonSerializer.Serialize(entries[i].Subject, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })} }}{(i < entries.Count - 1 ? "," : "")}\n");
        sb.Append("  ]\n}\n");
        return sb.ToString();
    }
}
