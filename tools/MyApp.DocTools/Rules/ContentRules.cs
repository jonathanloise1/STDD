using System.Text.RegularExpressions;
using MyApp.DocTools.Markdown;
using MyApp.DocTools.Model;

namespace MyApp.DocTools.Rules;

/// <summary>R06: field tables, closed vocabularies, role registry, feature references.</summary>
public sealed class R06FieldsAndVocabularies : Rule
{
    public override string Id => "R06";
    public override string Name => "Fields and vocabularies";

    private static readonly string[] StoryFields = { "Priority", "Feature", "User input" };
    private static readonly string[] CaseFields = { "User Story", "Type", "Priority", "Automated by" };
    private static readonly Regex SignedInWithRole = new(@"^Signed in as (?<role>[^""]+) of ""[^""]+""\.$", RegexOptions.Compiled);
    private static readonly Regex SignedInNoOrganization = new(@"^Signed in as ""[^""]+"", who belongs to no organization\.$", RegexOptions.Compiled);
    private static readonly Regex SecondSession = new(@"^A second session is signed in as (?<role>[^""]+) of ""[^""]+""\.$", RegexOptions.Compiled);
    private static readonly Regex ServiceFault = new(@"^The service does not answer when .+\.$", RegexOptions.Compiled);

    public override IEnumerable<Finding> Check(Repo repo)
    {
        var reg = repo.Registries;
        var rm = repo.Roadmap;
        foreach (var m in CheckRoadmap(repo)) yield return Found(Repo.RoadmapPath, m);

        foreach (var area in repo.Areas)
        {
            if (area.Index is not null)
            {
                foreach (var m in CheckAccess(repo, area.Index.SectionTable("Access Control"), atLeastOne: false))
                    yield return Found(area.Index.Path, m);
                foreach (var m in CheckHeader(area.Index.SectionTable("Stories"), "Stories", "ID", "Title", "Priority")) yield return Found(area.Index.Path, m);
                foreach (var m in CheckHeader(area.Index.SectionTable("Files"), "Files", "File", "Purpose")) yield return Found(area.Index.Path, m);
                var st = area.Index.SectionTable("Stories");
                if (st is not null)
                    foreach (var r in st.Rows.Where(r => r.Count >= 3 && !reg.Values("Priority").Contains(r[2])))
                        yield return Found(area.Index.Path, $"Stories table: Priority \"{r[2]}\" is not one of {Join(reg.Values("Priority"))}.");
            }
            if (area.Suite is not null)
            {
                var t = area.Suite.SectionTable("Coverage Matrix");
                foreach (var m in CheckHeader(t, "Coverage Matrix", "Test Case", "Title", "User Story", "Type", "Priority")) yield return Found(area.Suite.Path, m);
                if (t is not null)
                    foreach (var r in t.Rows.Where(r => r.Count >= 5))
                    {
                        if (!reg.Values("Type").Contains(r[3])) yield return Found(area.Suite.Path, $"Coverage Matrix: Type \"{r[3]}\" is not one of {Join(reg.Values("Type"))}.");
                        if (!reg.Values("Priority").Contains(r[4])) yield return Found(area.Suite.Path, $"Coverage Matrix: Priority \"{r[4]}\" is not one of {Join(reg.Values("Priority"))}.");
                    }
            }

            var indexNo = area.Index?.SectionTable("Access Control")?.Rows.Where(r => r.Count >= 2 && r[1] == "No").Select(r => r[0]).ToHashSet() ?? new();
            foreach (var s in area.Stories)
            {
                foreach (var m in CheckFields(s, StoryFields, s.HeadTable())) yield return Found(s.Path, m);
                if (s.Priority is { } p && !reg.Values("Priority").Contains(p)) yield return Found(s.Path, $"Priority \"{p}\" is not one of {Join(reg.Values("Priority"))}.");
                if (s.UserInput is { } ui && !reg.Values("User input").Contains(ui)) yield return Found(s.Path, $"User input \"{ui}\" is not one of {Join(reg.Values("User input"))}.");
                if (s.Feature is { } f)
                {
                    if (!Ids.Feature.IsMatch(f)) yield return Found(s.Path, $"Feature \"{f}\" does not match the grammar F-{{AREA}}-NN.");
                    else if (rm.Find(f) is null) yield return Found(s.Path, $"Feature {f} is not in the roadmap Features table ({Repo.RoadmapPath}). Add it there or fix the story.");
                }
                var at = s.SectionTable("Access Control");
                foreach (var m in CheckAccess(repo, at, atLeastOne: true)) yield return Found(s.Path, m);
                foreach (var r in s.AccessRows().Where(r => r.Allowed == "Yes" && indexNo.Contains(r.Role)))
                    yield return Found(s.Path, $"The story allows \"{r.Role}\" but the area index says \"{r.Role}\" may not enter the area.");
                if (!s.SectionIsNone("Error States"))
                    foreach (var m in CheckHeader(s.SectionTable("Error States"), "Error States", "Situation", "Message", "What the user can do")) yield return Found(s.Path, m);
            }

            foreach (var t in area.TestCases)
            {
                foreach (var m in CheckFields(t, CaseFields, t.HeadTable())) yield return Found(t.Path, m);
                if (t.Type is { } ty && !reg.Values("Type").Contains(ty)) yield return Found(t.Path, $"Type \"{ty}\" is not one of {Join(reg.Values("Type"))}.");
                if (t.Priority is { } pr && !reg.Values("Priority").Contains(pr)) yield return Found(t.Path, $"Priority \"{pr}\" is not one of {Join(reg.Values("Priority"))}.");
                var td = t.Doc.Section("Test Data");
                if (td is not null && !MarkdownDocument.IsNone(td))
                    foreach (var m in CheckHeader(t.SectionTable("Test Data"), "Test Data", "Field", "Value")) yield return Found(t.Path, m);
                foreach (var m in CheckPreconditions(t, reg)) yield return Found(t.Path, m);
                foreach (var m in CheckSteps(t, reg)) yield return Found(t.Path, m);
            }
        }
    }

    private static string Join(IEnumerable<string> v) => string.Join(", ", v.Select(x => $"\"{x}\""));

    private IEnumerable<string> CheckRoadmap(Repo repo)
    {
        var rm = repo.Roadmap;
        var reg = repo.Registries;
        if (rm.Header is null) yield break;
        if (!rm.Header.SequenceEqual(new[] { "ID", "Feature", "Area", "Status" }))
            yield return "The Features table must have exactly the columns ID, Feature, Area, Status.";
        foreach (var f in rm.Features)
        {
            if (!Ids.Feature.IsMatch(f.Id)) yield return $"Feature id \"{f.Id}\" does not match F-{{AREA}}-NN.";
            if (!reg.Values("Roadmap status").Contains(f.Status)) yield return $"Feature {f.Id}: Status \"{f.Status}\" is not one of {Join(reg.Values("Roadmap status"))}.";
            if (!repo.AreaFolders.Contains(f.Area)) yield return $"Feature {f.Id}: Area \"{f.Area}\" is not an area folder.";
            else if (repo.Areas.First(a => a.Folder == f.Area).Code is { } code && Ids.Feature.IsMatch(f.Id) && f.Id.Split('-')[1] != code)
                yield return $"Feature {f.Id}: its area code does not match the code {code} of area \"{f.Area}\".";
        }
        foreach (var g in rm.Features.GroupBy(f => f.Id).Where(g => g.Count() > 1))
            yield return $"Feature {g.Key} appears more than once.";
    }

    private static IEnumerable<string> CheckHeader(MdTable? t, string name, params string[] header)
    {
        if (t is null) yield break;
        if (!t.Header.SequenceEqual(header)) yield return $"{name} table: columns must be exactly {string.Join(" | ", header)}.";
    }

    private static IEnumerable<string> CheckFields(MdFile f, string[] expected, MdTable? t)
    {
        if (t is null) { yield return "The Field/Value table after the H1 is missing."; yield break; }
        if (!t.Header.SequenceEqual(new[] { "Field", "Value" })) yield return "The first table must have the columns Field | Value.";
        var actual = t.Rows.Select(r => r[0]).ToList();
        if (!actual.SequenceEqual(expected))
            yield return $"The field table must have exactly the fields {string.Join(", ", expected)} in this order; found {string.Join(", ", actual)}.";
    }

    private IEnumerable<string> CheckAccess(Repo repo, MdTable? t, bool atLeastOne)
    {
        if (t is null) { if (atLeastOne) yield return "The Access Control table is missing."; yield break; }
        if (!t.Header.SequenceEqual(new[] { "Role", "Allowed" })) yield return "Access Control table: columns must be exactly Role | Allowed.";
        if (atLeastOne && t.Rows.Count == 0) yield return "Access Control needs at least one row.";
        var roles = repo.Registries.Roles;
        var last = -1;
        foreach (var r in t.Rows.Where(r => r.Count >= 2))
        {
            var idx = roles.IndexOf(r[0]);
            if (idx < 0) yield return $"Access Control: \"{r[0]}\" is not a role of the registry ({Join(roles)}).";
            else if (idx <= last) yield return $"Access Control: \"{r[0]}\" is out of registry order or repeated; order is {string.Join(", ", roles)}.";
            else last = idx;
            if (!repo.Registries.Values("Allowed").Contains(r[1])) yield return $"Access Control: Allowed \"{r[1]}\" for \"{r[0]}\" must be Yes or No.";
        }
    }

    private static IEnumerable<string> CheckPreconditions(TestCaseFile t, Registries reg)
    {
        var lines = t.Doc.Section("Preconditions");
        if (lines is null) yield break;
        var bullets = MarkdownDocument.Bullets(lines);
        if (bullets.Count == 0) { yield return "Preconditions has no bullets."; yield break; }
        var first = bullets[0];
        if (first != "Not signed in." && !SignedInNoOrganization.IsMatch(first))
        {
            var m = SignedInWithRole.Match(first);
            if (!m.Success) yield return $"The first precondition must be 'Signed in as {{Role}} of \"{{Organization}}\".', 'Signed in as \"{{Person}}\", who belongs to no organization.' or 'Not signed in.'; found \"{first}\".";
            else if (!reg.Roles.Contains(m.Groups["role"].Value)) yield return $"The first precondition names the role \"{m.Groups["role"].Value}\", which is not in the role registry.";
        }
        foreach (var b in bullets.Skip(1))
        {
            if (b.StartsWith("A second session", StringComparison.Ordinal))
            {
                var s = SecondSession.Match(b);
                if (!s.Success) yield return $"A second actor is declared as 'A second session is signed in as {{Role}} of \"{{Organization}}\".'; found \"{b}\".";
                else if (!reg.Roles.Contains(s.Groups["role"].Value)) yield return $"The second session names the role \"{s.Groups["role"].Value}\", which is not in the role registry.";
            }
            else if (b.StartsWith("The service does not answer", StringComparison.Ordinal) && !ServiceFault.IsMatch(b))
                yield return $"A service fault is declared as 'The service does not answer when {{situation}}.'; found \"{b}\".";
        }
    }

    private static IEnumerable<string> CheckSteps(TestCaseFile t, Registries reg)
    {
        var lines = t.Doc.Section("Steps");
        if (lines is null) yield break;
        var steps = MarkdownDocument.NumberedItems(lines);
        if (steps.Count == 0) yield return "Steps has no numbered step.";
        var verbs = reg.Values("Step verbs");
        var n = 0;
        foreach (var raw in steps)
        {
            n++;
            var s = Regex.Replace(raw, @"^As [^:]+: ", "");
            if (!verbs.Any(v => s.StartsWith(v + " ", StringComparison.Ordinal) || s == v + "."))
                yield return $"Step {n} does not start with a step verb ({Join(verbs)}): \"{raw}\".";
        }
    }
}

/// <summary>R07: story size limits.</summary>
public sealed class R07StorySize : Rule
{
    public override string Id => "R07";
    public override string Name => "Story size";

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var s in repo.AllStories)
        {
            if (s.Doc.LineCount > 80) yield return Found(s.Path, $"The story has {s.Doc.LineCount} lines; the limit is 80. Split it into stories that each cover one user goal.");
            var ac = s.AcceptanceCount;
            if (ac < 1 || ac > 8) yield return Found(s.Path, $"The story has {ac} acceptance criteria; it needs 1 to 8.");
            foreach (var lang in new[] { "en", "it" })
            {
                var q = s.Questions(lang);
                if (q.Count is < 3 or > 5) yield return Found(s.Path, $"The story has {q.Count} questions under \"{lang}\"; it needs 3 to 5.");
                var title = s.H1Title;
                if (!string.IsNullOrEmpty(title))
                    foreach (var x in q.Where(x => x.Contains(title, StringComparison.OrdinalIgnoreCase)))
                        yield return Found(s.Path, $"The question \"{x}\" contains the story title; rephrase it in the user's words.");
            }
        }
    }
}

/// <summary>R08: banned content (7.3) in area indexes, stories and test cases.</summary>
public sealed class R08BannedContent : Rule
{
    public override string Id => "R08";
    public override string Name => "Banned content";

    private static readonly Regex Endpoint = new(@"/api/|\b(GET|POST|PUT|PATCH|DELETE) /", RegexOptions.Compiled);
    private static readonly Regex Status = new(@"\b[1-5]\d{2} (?:(?-i:OK)|(?:Created|Accepted|No Content|Bad Request|Unauthorized|Payment Required|Forbidden|Not Found|Method Not Allowed|Conflict|Gone|Unprocessable Entity|Too Many Requests|Internal Server Error|Bad Gateway|Service Unavailable|Gateway Timeout))\b|\bstatus codes?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TypeName = new(@"\b(?:[A-Z][a-z0-9]+)+(?:Dto|Request|Response|Controller|Service|Repository|Validator|Middleware|Interceptor|Entity)\b", RegexOptions.Compiled);
    private static readonly Regex SourceFile = new(@"\b\w+\.(?:cs|tsx|ts|json|sql)\b", RegexOptions.Compiled);
    private static readonly Regex Technical = new(@"\b(API|endpoint|DTO|JSON|HTTP|JWT|token|claim|GUID|database|localStorage|backend|frontend|payload|middleware|migration)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LinkTarget = new(@"\]\([^)]*\)", RegexOptions.Compiled);

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var area in repo.Areas)
        {
            if (area.Index is not null)
                foreach (var m in Scan(area.Index, Templates.IndexH2, false)) yield return Found(area.Index.Path, m);
            foreach (var s in area.Stories)
                foreach (var m in Scan(s, Templates.StoryH2.Concat(new[] { "en", "it" }), false)) yield return Found(s.Path, m);
            foreach (var t in area.TestCases)
                foreach (var m in Scan(t, Templates.CaseH2.Concat(new[] { Templates.TraceH2 }), true)) yield return Found(t.Path, m);
        }
    }

    private static IEnumerable<string> Scan(MdFile f, IEnumerable<string> allowedHeadings, bool isTestCase)
    {
        var allowed = allowedHeadings.ToHashSet();
        foreach (var h in f.Doc.Headings.Where(h => h.Level > 1 && !allowed.Contains(h.Text)))
            yield return $"Line {h.Line + 1}: heading \"{h.Text}\" is not in the template (implementation sections are banned).";
        for (var i = 0; i < f.Doc.Lines.Length; i++)
        {
            var line = f.Doc.Lines[i];
            var cells = line.TrimStart().StartsWith('|') ? MarkdownDocument.SplitRow(line) : null;
            var isLogKeyRow = isTestCase && cells is { Count: > 0 } && cells[0] == "Log key";
            var isRunbookRow = isTestCase && cells is { Count: > 0 } && cells[0] == "Runbook";
            var at = $"Line {i + 1}: ";
            if (line.Contains('`') && !isLogKeyRow) yield return at + "backtick (code span or code block) is banned" + (isTestCase ? " except in the Log key value." : ".");
            if (isLogKeyRow || isRunbookRow) continue;
            var text = LinkTarget.Replace(line, "]");
            if (Endpoint.Match(text) is { Success: true } e) yield return at + $"API endpoint \"{e.Value}\" is banned.";
            if (Status.Match(text) is { Success: true } s) yield return at + $"HTTP status code \"{s.Value}\" is banned.";
            if (TypeName.Match(text) is { Success: true } t) yield return at + $"type or layer name \"{t.Value}\" is banned.";
            if (SourceFile.Match(text) is { Success: true } sf) yield return at + $"source file name \"{sf.Value}\" is banned.";
            foreach (Match m in Technical.Matches(text)) yield return at + $"technical word \"{m.Value}\" is banned; describe what the user sees.";
        }
    }
}
