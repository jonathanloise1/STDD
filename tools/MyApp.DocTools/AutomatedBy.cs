using System.Text.RegularExpressions;
using MyApp.DocTools.Model;

namespace MyApp.DocTools;

/// <summary>Derives and rewrites the "Automated by" field of test cases (development.md 8.4).</summary>
public static class AutomatedBy
{
    /// <summary>For every test case id referenced by a TestCase tag: the derived value.</summary>
    public static string Derive(Repo repo, string testCaseId)
    {
        var classes = repo.TestClasses.Where(c => c.Level is TestLevel.E2E or TestLevel.Integration && c.Class.TestCaseTags.Contains(testCaseId)).ToList();
        var items = classes.Where(c => c.Level == TestLevel.E2E).Select(c => "e2e/" + c.Name).Distinct().OrderBy(x => x, StringComparer.Ordinal)
            .Concat(classes.Where(c => c.Level == TestLevel.Integration).Select(c => "integration/" + c.Name).Distinct().OrderBy(x => x, StringComparer.Ordinal))
            .ToList();
        return items.Count == 0 ? "None" : string.Join(", ", items);
    }

    private static readonly Regex RowRx = new(@"^\|[ \t]*Automated by[ \t]*\|[^\r\n]*", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Rewrites the row in every test case file; returns the files that changed.</summary>
    public static List<string> Sync(Repo repo)
    {
        var changed = new List<string>();
        foreach (var t in repo.AllTestCases)
        {
            var derived = Derive(repo, t.Stem);
            var text = File.ReadAllText(repo.Abs(t.Path), new System.Text.UTF8Encoding(false));
            var updated = RowRx.Replace(text, $"| Automated by | {derived} |", 1);
            if (updated == text) continue;
            File.WriteAllText(repo.Abs(t.Path), updated, new System.Text.UTF8Encoding(false));
            changed.Add(t.Path);
        }
        return changed;
    }
}
