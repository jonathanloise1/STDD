using System.Text.RegularExpressions;
using MyApp.DocTools.Markdown;
using MyApp.DocTools.Model;

namespace MyApp.DocTools.Rules;

/// <summary>R09: indexes list exactly the files of the area, with equal titles, stories, types and priorities; links resolve.</summary>
public sealed class R09IndexesComplete : Rule
{
    public override string Id => "R09";
    public override string Name => "Indexes complete";

    private static readonly Regex LinkCell = new(@"^\[([^\]]+)\]\(([^)]+)\)$", RegexOptions.Compiled);

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var area in repo.Areas)
        {
            if (area.Index is not null)
            {
                foreach (var m in CheckStories(area)) yield return Found(area.Index.Path, m);
                foreach (var m in CheckFiles(area)) yield return Found(area.Index.Path, m);
                foreach (var m in CheckLinks(repo, area.Index)) yield return Found(area.Index.Path, m);
            }
            if (area.Suite is not null)
            {
                foreach (var m in CheckMatrix(area)) yield return Found(area.Suite.Path, m);
                foreach (var m in CheckLinks(repo, area.Suite)) yield return Found(area.Suite.Path, m);
            }
        }
    }

    private static IEnumerable<string> CheckStories(Area area)
    {
        var t = area.Index!.SectionTable("Stories");
        if (t is null) yield break;
        var rows = new List<string>();
        foreach (var r in t.Rows)
        {
            var m = r.Count > 0 ? LinkCell.Match(r[0]) : null;
            if (m is null || !m.Success) { yield return $"Stories table: the first cell \"{(r.Count > 0 ? r[0] : "")}\" is not a link [US-ID](user-stories/US-ID.md)."; continue; }
            var id = m.Groups[1].Value;
            rows.Add(id);
            if (m.Groups[2].Value != $"user-stories/{id}.md") yield return $"Stories table: the link of {id} must point to user-stories/{id}.md.";
            var story = area.Stories.FirstOrDefault(s => s.Stem == id);
            if (story is null) { yield return $"Stories table lists {id} but there is no such story file."; continue; }
            if (r.Count >= 3)
            {
                if (r[1] != story.H1Title) yield return $"Stories table: title of {id} is \"{r[1]}\" but the story says \"{story.H1Title}\".";
                if (r[2] != story.Priority) yield return $"Stories table: priority of {id} is \"{r[2]}\" but the story says \"{story.Priority}\".";
            }
        }
        foreach (var s in area.Stories.Where(s => !rows.Contains(s.Stem))) yield return $"Story file {s.Stem} has no row in the Stories table.";
        if (!rows.SequenceEqual(rows.OrderBy(x => x, StringComparer.Ordinal))) yield return "Stories table rows are not in id order.";
    }

    private static IEnumerable<string> CheckMatrix(Area area)
    {
        var t = area.Suite!.SectionTable("Coverage Matrix");
        if (t is null) yield break;
        var rows = new List<(string Story, string Id)>();
        foreach (var r in t.Rows)
        {
            var m = r.Count > 0 ? LinkCell.Match(r[0]) : null;
            if (m is null || !m.Success) { yield return $"Coverage Matrix: the first cell \"{(r.Count > 0 ? r[0] : "")}\" is not a link [ID](test-cases/ID.md)."; continue; }
            var id = m.Groups[1].Value;
            rows.Add((r.Count > 2 ? r[2] : "", id));
            if (m.Groups[2].Value != $"test-cases/{id}.md") yield return $"Coverage Matrix: the link of {id} must point to test-cases/{id}.md.";
            var tc = area.TestCases.FirstOrDefault(x => x.Stem == id);
            if (tc is null) { yield return $"Coverage Matrix lists {id} but there is no such test case file."; continue; }
            if (r.Count >= 5)
            {
                if (r[1] != tc.H1Title) yield return $"Coverage Matrix: title of {id} is \"{r[1]}\" but the test case says \"{tc.H1Title}\".";
                if (r[2] != tc.UserStory) yield return $"Coverage Matrix: user story of {id} is \"{r[2]}\" but the test case says \"{tc.UserStory}\".";
                if (r[3] != tc.Type) yield return $"Coverage Matrix: type of {id} is \"{r[3]}\" but the test case says \"{tc.Type}\".";
                if (r[4] != tc.Priority) yield return $"Coverage Matrix: priority of {id} is \"{r[4]}\" but the test case says \"{tc.Priority}\".";
            }
        }
        foreach (var tc in area.TestCases.Where(x => rows.All(r => r.Id != x.Stem))) yield return $"Test case file {tc.Stem} has no row in the Coverage Matrix.";
        if (!rows.SequenceEqual(rows.OrderBy(x => x.Story, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal)))
            yield return "Coverage Matrix rows are not ordered by user story, then test case id.";
    }

    private static IEnumerable<string> CheckFiles(Area area)
    {
        var t = area.Index!.SectionTable("Files");
        var targets = t?.Rows.Where(r => r.Count > 0).Select(r => LinkCell.Match(r[0])).Where(m => m.Success).Select(m => m.Groups[2].Value).ToList() ?? new();
        if (!targets.Contains("testsuite.md")) yield return "Files table does not list testsuite.md.";
        if (area.Operations is not null && !targets.Contains("operations.md")) yield return "Files table does not list operations.md, which exists.";
        if (area.Operations is null && targets.Contains("operations.md")) yield return "Files table lists operations.md, which does not exist; remove the row.";
    }

    private static IEnumerable<string> CheckLinks(Repo repo, MdFile file)
    {
        for (var i = 0; i < file.Doc.Lines.Length; i++)
        {
            if (file.Doc.InFence[i]) continue;
            foreach (Match m in MarkdownDocument.LinkRx.Matches(file.Doc.Lines[i]))
            {
                var target = m.Groups[2].Value.Trim();
                if (target.Length == 0 || target.StartsWith('#') || Regex.IsMatch(target, @"^[a-z]+:", RegexOptions.IgnoreCase)) continue;
                var path = target.Split('#')[0];
                var rel = Normalize(file.Dir + "/" + path);
                if (rel is null || !repo.FileExists(rel)) yield return $"Line {i + 1}: the link \"{target}\" does not resolve to a file.";
            }
        }
    }

    internal static string? Normalize(string rel)
    {
        var stack = new List<string>();
        foreach (var p in rel.Split('/'))
        {
            if (p is "" or ".") continue;
            if (p == "..") { if (stack.Count == 0) return null; stack.RemoveAt(stack.Count - 1); }
            else stack.Add(p);
        }
        return string.Join("/", stack);
    }
}

/// <summary>R10: User Story names a story file in the same area.</summary>
public sealed class R10TestCaseToStory : Rule
{
    public override string Id => "R10";
    public override string Name => "Test case to story";

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var area in repo.Areas)
            foreach (var t in area.TestCases)
            {
                var us = t.UserStory;
                if (string.IsNullOrWhiteSpace(us)) yield return Found(t.Path, "The test case has no User Story field.");
                else if (area.Stories.All(s => s.Stem != us))
                    yield return Found(t.Path, $"User Story \"{us}\" is not a story file of the area \"{area.Folder}\".");
            }
    }
}

/// <summary>R11: the coverage rules of 8.6 decided by test case Type. The Error States row is R12.</summary>
public sealed class R11Coverage : Rule
{
    public override string Id => "R11";
    public override string Name => "Coverage";

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var area in repo.Areas)
            foreach (var s in area.Stories)
            {
                var cases = area.TestCasesOf(s.Stem).ToList();
                bool Has(string type) => cases.Any(c => c.Type == type);
                if (!Has("Happy path")) yield return Found(s.Path, "The story has no test case of type Happy path.");
                if (s.UserInput == "Yes" && !Has("Validation")) yield return Found(s.Path, "User input is Yes but the story has no test case of type Validation.");
                if (!s.SectionIsNone("Business Rules") && s.Doc.Section("Business Rules") is not null && !Has("Business rule"))
                    yield return Found(s.Path, "The story has Business Rules but no test case of type Business rule.");
                if (s.AccessRows().Any(r => r.Allowed == "No") && !Has("Security"))
                    yield return Found(s.Path, "An Access Control row is No but the story has no test case of type Security.");
            }
    }
}

/// <summary>R12: each Error States message equals an Observable trace message of a test case of the same story.</summary>
public sealed class R12ErrorStatesCovered : Rule
{
    public override string Id => "R12";
    public override string Name => "Error States covered";

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var area in repo.Areas)
            foreach (var s in area.Stories)
            {
                if (s.SectionIsNone("Error States")) continue;
                var messages = area.TestCasesOf(s.Stem).Where(c => c.HasTraceSection).Select(c => c.TraceMessage).ToList();
                foreach (var row in s.ErrorRows())
                    if (!messages.Contains(row.Message))
                        yield return Found(s.Path, $"The Error States message \"{row.Message}\" is not the Message of any Observable trace of this story's test cases. Add a test case with that trace, or fix the text.");
            }
    }
}
