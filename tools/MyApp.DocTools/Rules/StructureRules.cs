using System.Text;
using System.Text.RegularExpressions;
using MyApp.DocTools.Markdown;
using MyApp.DocTools.Model;

namespace MyApp.DocTools.Rules;

/// <summary>R01: the folders under docs/areas and the rows of the area registry are the same set.</summary>
public sealed class R01AreaRegistry : Rule
{
    public override string Id => "R01";
    public override string Name => "Area registry";

    public override IEnumerable<Finding> Check(Repo repo)
    {
        var reg = repo.Registries;
        if (!reg.Loaded)
        {
            yield return Found(Repo.DevelopmentPath, "The area registry cannot be read: this file is missing.");
            yield break;
        }
        var folders = repo.Areas.Select(a => a.Folder).ToHashSet();
        foreach (var f in folders.Where(f => reg.Areas.All(r => r.Folder != f)).OrderBy(f => f, StringComparer.Ordinal))
            yield return Found($"docs/areas/{f}", $"The folder has no row in the area registry (section 3.2). Add a row with its area code and title to {Repo.DevelopmentPath}, or remove the folder.");
        foreach (var r in reg.Areas.Where(r => !folders.Contains(r.Folder)))
            yield return Found($"docs/areas/{r.Folder}", $"The area registry lists \"{r.Folder}\" but the folder does not exist. Create the area or remove the row.");
        foreach (var g in reg.Areas.GroupBy(r => r.Folder).Where(g => g.Count() > 1))
            yield return Found($"docs/areas/{g.Key}", "The area registry lists this folder more than once.");
        foreach (var g in reg.Areas.GroupBy(r => r.Code).Where(g => g.Count() > 1))
            foreach (var r in g)
                yield return Found($"docs/areas/{r.Folder}", $"The area code {r.Code} is used by more than one registry row; codes are unique.");
        foreach (var r in reg.Areas)
        {
            if (!Ids.AreaFolder.IsMatch(r.Folder))
                yield return Found($"docs/areas/{r.Folder}", $"The area folder name \"{r.Folder}\" does not match {Ids.AreaFolder}.");
            if (!Ids.AreaCode.IsMatch(r.Code))
                yield return Found($"docs/areas/{r.Folder}", $"The area code \"{r.Code}\" does not match {Ids.AreaCode}.");
        }
    }
}

/// <summary>R02: an area contains only the files of section 4.</summary>
public sealed class R02AllowedFiles : Rule
{
    public override string Id => "R02";
    public override string Name => "Allowed files";

    private static readonly HashSet<string> TopFiles = new() { "userstories.md", "testsuite.md", "operations.md" };

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var area in repo.Areas)
        {
            var prefix = area.Path + "/";
            foreach (var dir in repo.EnumerateDirs(area.Path))
            {
                var rel = dir[prefix.Length..];
                if (rel is "user-stories" or "test-cases") continue;
                yield return Found(dir, $"Unexpected folder in an area. An area has only the folders user-stories and test-cases (section 4); move or remove \"{rel}\".");
            }
            foreach (var file in repo.EnumerateFiles(area.Path, skipBuildDirs: false))
            {
                var rel = file[prefix.Length..];
                var parts = rel.Split('/');
                var ok = parts.Length switch
                {
                    1 => TopFiles.Contains(parts[0]),
                    2 => parts[0] is "user-stories" or "test-cases" && parts[1].EndsWith(".md", StringComparison.Ordinal),
                    _ => false,
                };
                if (!ok) yield return Found(file, $"File not allowed in an area (section 4): \"{rel}\". Allowed: userstories.md, testsuite.md, operations.md, user-stories/*.md, test-cases/*.md.");
            }
        }
    }
}

/// <summary>R03: every docs/**/*.md is UTF-8 without BOM, LF only, with no U+FFFD and none of the symbols banned by 1.4.</summary>
public sealed class R03Encoding : Rule
{
    public override string Id => "R03";
    public override string Name => "Encoding";

    private static string? BannedSymbol(char c) => c switch
    {
        >= '\u2500' and <= '\u257F' => "a box-drawing character; draw with +, - and |",
        >= '\u2190' and <= '\u21FF' => "an arrow character; write ->",
        '\u2610' or '\u2611' or '\u2612' or '\u2705' or '\u2713' or '\u2714' or '\u2715' or '\u2716' or '\u2717' or '\u2718' or '\u274C' or '\u274E'
            => "a check or cross mark; write the word",
        _ => null,
    };

    private static IEnumerable<string> BannedSymbols(string text)
    {
        var seen = new HashSet<string>();
        var line = 1;
        foreach (var c in text)
        {
            if (c == '\n') line++;
            if (BannedSymbol(c) is { } what && seen.Add(what))
                yield return $"Line {line}: \"{c}\" is {what} (section 1.4).";
        }
    }

    private static bool TryDecode(byte[] bytes, out string text)
    {
        try { text = new UTF8Encoding(false, true).GetString(bytes); return true; }
        catch (DecoderFallbackException) { text = Encoding.UTF8.GetString(bytes); return false; }
    }

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var file in repo.EnumerateFiles("docs", skipBuildDirs: false).Where(f => f.EndsWith(".md", StringComparison.Ordinal)))
        {
            var bytes = File.ReadAllBytes(repo.Abs(file));
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                yield return Found(file, "The file starts with a byte order mark. Save it as UTF-8 without BOM.");
            var bad = !TryDecode(bytes, out var text);
            if (bad) yield return Found(file, "The file is not valid UTF-8. Re-save it as UTF-8.");
            if (text.Contains('\r'))
                yield return Found(file, "The file has CR characters (CRLF line endings). Convert it to LF.");
            if (!bad && text.Contains('�'))
                yield return Found(file, "The file contains the replacement character U+FFFD, a sign of a broken encoding conversion. Restore the original character.");
            foreach (var m in BannedSymbols(text)) yield return Found(file, m);
        }
    }
}

/// <summary>R04: area index, story, test case and test suite have exactly their headings, in order; the roadmap has the Features table.</summary>
public sealed class R04RequiredHeadings : Rule
{
    public override string Id => "R04";
    public override string Name => "Required headings";

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var area in repo.Areas)
        {
            if (area.Index is null)
                yield return Found($"{area.Path}/userstories.md", "The area index is missing (section 5).");
            else
            {
                foreach (var m in Compare(area.Index, Templates.Flat(Templates.IndexH2), h1: Regex.Escape(area.Registry?.Title ?? ".+") + " - User Stories", h1Hint: $"# {area.Registry?.Title ?? "{Area title}"} - User Stories"))
                    yield return Found(area.Index.Path, m);
            }
            if (area.Suite is null)
                yield return Found($"{area.Path}/testsuite.md", "The test suite is missing (section 9).");
            else
            {
                foreach (var m in Compare(area.Suite, Templates.Flat(Templates.SuiteH2), h1: Regex.Escape(area.Registry?.Title ?? ".+") + " - Test Suite", h1Hint: $"# {area.Registry?.Title ?? "{Area title}"} - Test Suite"))
                    yield return Found(area.Suite.Path, m);
            }
            foreach (var s in area.Stories)
                foreach (var m in Compare(s, Templates.StoryHeadings(), h1: @"\S+: \S.*", h1Hint: "# US-{AREA}-NN: {Title}"))
                    yield return Found(s.Path, m);
            foreach (var t in area.TestCases)
            {
                var expected = Templates.Flat(Templates.CaseH2);
                if (t.HasTraceSection) expected.Add((2, Templates.TraceH2));
                foreach (var m in Compare(t, expected, h1: @"\S+: \S.*", h1Hint: "# {AREA}-{ACTION}-NNN: {Title}"))
                    yield return Found(t.Path, m);
            }
        }

        var rm = repo.Roadmap;
        if (rm.File is null) yield return Found(Repo.RoadmapPath, "The roadmap is missing.");
        else if (!rm.HasFeaturesSection) yield return Found(Repo.RoadmapPath, "The roadmap has no \"## Features\" section (section 3.3).");
        else if (rm.Header is null) yield return Found(Repo.RoadmapPath, "The \"## Features\" section holds no table. Use the columns ID, Feature, Area, Status.");
    }

    private static IEnumerable<string> Compare(MdFile file, List<(int Level, string Text)> expected, string h1, string h1Hint)
    {
        var h1s = file.Doc.Headings.Where(h => h.Level == 1).ToList();
        if (h1s.Count != 1) yield return $"The file must have exactly one H1 (\"{h1Hint}\"); found {h1s.Count}.";
        else
        {
            if (file.Doc.Headings[0].Level != 1) yield return "The H1 must be the first heading.";
            if (!Regex.IsMatch(h1s[0].Text, "^" + h1 + "$")) yield return $"The H1 \"{h1s[0].Text}\" does not follow the pattern \"{h1Hint}\".";
        }
        var actual = file.Doc.Headings.Where(h => h.Level != 1).Select(h => (h.Level, h.Text)).ToList();
        var ok = true;
        foreach (var h in actual.Where(a => !expected.Contains(a)))
        { ok = false; yield return $"Unexpected heading \"{new string('#', h.Level)} {h.Text}\": only the headings of the template are allowed."; }
        foreach (var h in expected.Where(e => !actual.Contains(e)))
        { ok = false; yield return $"Missing heading \"{new string('#', h.Level)} {h.Text}\"."; }
        foreach (var g in actual.GroupBy(a => a).Where(g => g.Count() > 1))
        { ok = false; yield return $"Heading \"{new string('#', g.Key.Level)} {g.Key.Text}\" appears {g.Count()} times."; }
        if (ok && !actual.SequenceEqual(expected))
            yield return "The headings are not in template order. Expected: " + string.Join(", ", expected.Select(e => e.Text)) + ".";
    }
}

/// <summary>R05: the H1 id matches its grammar, equals the file name, and its area code is the registry code of the folder.</summary>
public sealed class R05IdsAndFileNames : Rule
{
    public override string Id => "R05";
    public override string Name => "Ids and file names";

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var area in repo.Areas)
        {
            foreach (var s in area.Stories)
                foreach (var m in Verify(s, Ids.Story, "US-{AREA}-NN", area, 1))
                    yield return Found(s.Path, m);
            foreach (var t in area.TestCases)
                foreach (var m in Verify(t, Ids.TestCase, "{AREA}-{ACTION}-NNN", area, 0))
                    yield return Found(t.Path, m);
        }
    }

    private static IEnumerable<string> Verify(MdFile f, Regex grammar, string shape, Area area, int codeSegment)
    {
        var id = f.H1Id;
        if (id is null) { yield return $"The H1 does not start with an id followed by a colon (expected \"# {shape}: {{Title}}\")."; yield break; }
        if (!grammar.IsMatch(id)) yield return $"The H1 id \"{id}\" does not match the grammar {shape} (section 3.1).";
        if (id != f.Stem) yield return $"The H1 id \"{id}\" differs from the file name \"{f.Stem}.md\". Rename the file or fix the H1.";
        if (!grammar.IsMatch(f.Stem)) yield return $"The file name \"{f.Stem}\" does not match the id grammar {shape}.";
        if (area.Code is { } code)
        {
            var parts = f.Stem.Split('-');
            var actual = codeSegment == 1 ? (parts.Length > 1 ? parts[1] : "") : parts[0];
            if (actual != code) yield return $"The area code in the id is \"{actual}\" but the folder \"{area.Folder}\" has the code \"{code}\".";
        }
    }
}
