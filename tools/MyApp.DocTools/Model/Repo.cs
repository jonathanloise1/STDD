using System.Text;
using System.Text.RegularExpressions;
using MyApp.DocTools.CSharp;
using MyApp.DocTools.Markdown;

namespace MyApp.DocTools.Model;

public enum TestLevel { Unit, Integration, E2E, Capture, Monitor }

public sealed record AreaRegistryRow(string Folder, string Code, string Title);
public sealed record Feature(string Id, string Name, string Area, string Status);

/// <summary>A Markdown file of the repository.</summary>
public class MdFile
{
    public string Path { get; }
    public string Text { get; }
    public MarkdownDocument Doc { get; }
    public string Stem => System.IO.Path.GetFileNameWithoutExtension(Path);
    public string Dir => Path.Contains('/') ? Path[..Path.LastIndexOf('/')] : "";

    public MdFile(string path, string text)
    {
        Path = path;
        Text = text;
        Doc = MarkdownDocument.Parse(text);
    }

    private static readonly Regex H1Rx = new(@"^(\S+?):\s+(.+)$");

    /// <summary>The id written in the H1 of stories and test cases, or null.</summary>
    public string? H1Id => H1Rx.Match(Doc.H1?.Text ?? "") is { Success: true } m ? m.Groups[1].Value : null;
    public string? H1Title => H1Rx.Match(Doc.H1?.Text ?? "") is { Success: true } m ? m.Groups[2].Value : null;

    /// <summary>First table of the section between the H1 and the first H2: (Field, Value) rows.</summary>
    public List<(string Field, string Value)> HeadFields()
    {
        var t = MarkdownDocument.ParseTables(Doc.HeadLines()).FirstOrDefault();
        return t is null ? new() : t.Rows.Where(r => r.Count >= 2).Select(r => (r[0], r[1])).ToList();
    }

    public MdTable? HeadTable() => MarkdownDocument.ParseTables(Doc.HeadLines()).FirstOrDefault();

    public string? Field(string name)
    {
        foreach (var f in HeadFields()) if (f.Field == name) return f.Value;
        return null;
    }

    public MdTable? SectionTable(string h2) =>
        Doc.Section(h2) is { } lines ? MarkdownDocument.ParseTables(lines).FirstOrDefault() : null;
}

public sealed class StoryFile : MdFile
{
    public Area Area { get; }
    public StoryFile(Area area, string path, string text) : base(path, text) { Area = area; }

    public string? Priority => Field("Priority");
    public string? Feature => Field("Feature");
    public string? UserInput => Field("User input");

    public List<(string Role, string Allowed)> AccessRows() =>
        SectionTable("Access Control")?.Rows.Where(r => r.Count >= 2).Select(r => (r[0], r[1])).ToList() ?? new();

    public bool SectionIsNone(string h2) => Doc.Section(h2) is { } l && MarkdownDocument.IsNone(l);

    public int AcceptanceCount => Doc.Section("Acceptance Criteria") is { } l ? MarkdownDocument.NumberedItems(l).Count : 0;

    /// <summary>Rows of the Error States table: (Situation, Message without quotes, Action). Empty when None.</summary>
    public List<(string Situation, string Message, string Action)> ErrorRows()
    {
        var t = SectionTable("Error States");
        if (t is null) return new();
        return t.Rows.Where(r => r.Count >= 3).Select(r => (r[0], MarkdownDocument.Unquote(r[1]), r[2])).ToList();
    }

    public List<string> Questions(string lang)
    {
        var q = Doc.Find(2, "Questions users ask");
        if (q is null) return new();
        var h3 = Doc.Headings.FirstOrDefault(h => h.Level == 3 && h.Text == lang && h.Line > q.Line);
        if (h3 is null) return new();
        return MarkdownDocument.NumberedItems(Doc.SectionLines(h3));
    }
}

public sealed class TestCaseFile : MdFile
{
    public Area Area { get; }
    public TestCaseFile(Area area, string path, string text) : base(path, text) { Area = area; }

    public string? UserStory => Field("User Story");
    public string? Type => Field("Type");
    public string? Priority => Field("Priority");
    public string? AutomatedBy => Field("Automated by");

    public bool NeedsTrace => Type is "Edge case" or "Error handling";
    public bool HasTraceSection => Doc.Find(2, "Observable trace") is not null;

    public List<(string Field, string Value)> TraceRows() =>
        SectionTable("Observable trace")?.Rows.Where(r => r.Count >= 2).Select(r => (r[0], r[1])).ToList() ?? new();

    public string? TraceValue(string field)
    {
        foreach (var r in TraceRows()) if (r.Field == field) return r.Value;
        return null;
    }

    /// <summary>The log key in backticks of the Observable trace, or null.</summary>
    public string? LogKey
    {
        get
        {
            var v = TraceValue("Log key");
            if (v is null) return null;
            var m = Regex.Match(v, @"^`([^`]+)`$");
            return m.Success ? m.Groups[1].Value : null;
        }
    }

    /// <summary>Message of the Observable trace without quotes; "None" stays "None".</summary>
    public string? TraceMessage => TraceValue("Message") is { } v ? MarkdownDocument.Unquote(v) : null;

    /// <summary>Id parts from the file name: area code, action, number.</summary>
    public (string Area, string Action, string Number)? IdParts()
    {
        var p = Stem.Split('-');
        return p.Length == 3 ? (p[0], p[1], p[2]) : null;
    }
}

public sealed class Area
{
    public string Folder { get; init; } = "";
    public string Path => $"docs/areas/{Folder}";
    public AreaRegistryRow? Registry { get; init; }
    public string? Code => Registry?.Code;
    public MdFile? Index { get; set; }
    public MdFile? Suite { get; set; }
    public MdFile? Operations { get; set; }
    public List<StoryFile> Stories { get; } = new();
    public List<TestCaseFile> TestCases { get; } = new();

    public IEnumerable<TestCaseFile> TestCasesOf(string storyId) =>
        TestCases.Where(t => t.UserStory == storyId);
}

public sealed class Registries
{
    public bool Loaded { get; init; }
    public List<AreaRegistryRow> Areas { get; init; } = new();
    public List<string> Roles { get; init; } = new();
    public Dictionary<string, List<string>> Vocab { get; init; } = new();
    public List<string> ContractKeys { get; init; } = new();

    public IReadOnlyList<string> Values(string field) =>
        Vocab.TryGetValue(field, out var v) ? v : Array.Empty<string>();
}

public sealed class Roadmap
{
    public MdFile? File { get; init; }
    public bool HasFeaturesSection { get; init; }
    public List<string>? Header { get; init; }
    public List<Feature> Features { get; init; } = new();
    public Feature? Find(string id) => Features.FirstOrDefault(f => f.Id == id);
}

/// <summary>A C# class found under tests/ with its level and location.</summary>
public sealed class TestClassInfo
{
    public string Path { get; init; } = "";
    public TestLevel Level { get; init; }
    public CsClass Class { get; init; } = null!;
    public string Name => Class.Name;
}

public sealed class Repo
{
    public const string DevelopmentPath = "docs/guidelines/development.md";
    public const string RoadmapPath = "docs/roadmap.md";
    public const string ContractsPath = "docs/contracts.json";
    public const string BaselinePath = "tests/unit/MyApp.UnitTests/Architecture/baseline.json";

    private static readonly HashSet<string> ExcludedDirs = new(StringComparer.Ordinal)
    {
        "node_modules", "bin", "obj", "dist", ".git", ".vite", ".vs", ".idea", "coverage", ".next", "playwright-report", "test-results",
    };

    /// <summary>
    /// Folders written by build and release commands (git-ignored): generated help packs, model files served to the
    /// browser and the production build. They are copies of sources the rules already check.
    /// </summary>
    private static readonly HashSet<string> ExcludedGeneratedPaths = new(StringComparer.Ordinal)
    {
        "src/frontend/build", "src/frontend/public/help", "src/frontend/public/models",
    };

    public string Root { get; }

    private readonly Lazy<Registries> registries;
    private readonly Lazy<Roadmap> roadmap;
    private readonly Lazy<List<Area>> areas;
    private readonly Lazy<List<TestClassInfo>> testClasses;
    private readonly Lazy<List<CsFile>> backend;

    public Repo(string root)
    {
        Root = System.IO.Path.GetFullPath(root);
        registries = new(LoadRegistries);
        roadmap = new(LoadRoadmap);
        areas = new(LoadAreas);
        testClasses = new(LoadTestClasses);
        backend = new(LoadBackend);
    }

    /// <summary>Walks up from a folder to the first one that contains docs/contracts.json.</summary>
    public static string? FindRoot(string start)
    {
        var dir = new DirectoryInfo(System.IO.Path.GetFullPath(start));
        while (dir is not null)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "docs", "contracts.json"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    public string Abs(string rel) => System.IO.Path.Combine(Root, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
    public bool FileExists(string rel) => File.Exists(Abs(rel));
    public bool DirExists(string rel) => Directory.Exists(Abs(rel));
    public string? ReadText(string rel) => FileExists(rel) ? File.ReadAllText(Abs(rel), new UTF8Encoding(false)) : null;

    public MdFile? LoadMd(string rel) => ReadText(rel) is { } t ? new MdFile(rel, t) : null;

    /// <summary>All files under a repository-relative folder (recursive), as relative paths, sorted ordinally.</summary>
    public List<string> EnumerateFiles(string relDir, bool skipBuildDirs = true)
    {
        var result = new List<string>();
        var abs = Abs(relDir);
        if (!Directory.Exists(abs)) return result;
        Walk(abs, relDir.TrimEnd('/'), skipBuildDirs, result, files: true);
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    public List<string> EnumerateDirs(string relDir)
    {
        var result = new List<string>();
        var abs = Abs(relDir);
        if (!Directory.Exists(abs)) return result;
        Walk(abs, relDir.TrimEnd('/'), false, result, files: false);
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static void Walk(string abs, string rel, bool skipBuild, List<string> acc, bool files)
    {
        foreach (var d in Directory.GetDirectories(abs))
        {
            var name = System.IO.Path.GetFileName(d);
            if (skipBuild && ExcludedDirs.Contains(name)) continue;
            if (skipBuild && ExcludedGeneratedPaths.Contains($"{rel}/{name}")) continue;
            var r = $"{rel}/{name}";
            if (!files) acc.Add(r);
            Walk(d, r, skipBuild, acc, files);
        }
        if (files)
            foreach (var f in Directory.GetFiles(abs)) acc.Add($"{rel}/{System.IO.Path.GetFileName(f)}");
    }

    public Registries Registries => registries.Value;
    public Roadmap Roadmap => roadmap.Value;
    public IReadOnlyList<Area> Areas => areas.Value;
    public IReadOnlyList<TestClassInfo> TestClasses => testClasses.Value;
    public IReadOnlyList<CsFile> BackendSources => backend.Value;

    public IEnumerable<StoryFile> AllStories => Areas.SelectMany(a => a.Stories);
    public IEnumerable<TestCaseFile> AllTestCases => Areas.SelectMany(a => a.TestCases);
    public HashSet<string> AreaFolders => Areas.Select(a => a.Folder).ToHashSet();

    private Registries LoadRegistries()
    {
        var md = LoadMd(DevelopmentPath);
        if (md is null) return new Registries { Loaded = false };
        var doc = md.Doc;

        List<List<string>> RowsUnder(string prefix)
        {
            var h = doc.FindStartingWith(prefix);
            if (h is null) return new();
            var t = MarkdownDocument.ParseTables(doc.SectionLines(h)).FirstOrDefault();
            return t?.Rows ?? new();
        }

        static string Strip(string s) => s.Trim().Trim('`');

        var areasRows = RowsUnder("3.2 ").Where(r => r.Count >= 3).Select(r => new AreaRegistryRow(Strip(r[0]), Strip(r[1]), r[2])).ToList();
        var roles = RowsUnder("3.4 ").Where(r => r.Count >= 1).Select(r => Strip(r[0])).ToList();
        var vocab = new Dictionary<string, List<string>>();
        foreach (var r in RowsUnder("3.5 ").Where(r => r.Count >= 2))
            vocab[r[0]] = Regex.Matches(r[1], "`([^`]+)`").Select(m => m.Groups[1].Value).ToList();
        var keys = RowsUnder("13. ").Where(r => r.Count >= 1).Select(r => Strip(r[0])).ToList();
        return new Registries { Loaded = true, Areas = areasRows, Roles = roles, Vocab = vocab, ContractKeys = keys };
    }

    private Roadmap LoadRoadmap()
    {
        var md = LoadMd(RoadmapPath);
        if (md is null) return new Roadmap();
        var h = md.Doc.Find(2, "Features");
        if (h is null) return new Roadmap { File = md };
        var t = MarkdownDocument.ParseTables(md.Doc.SectionLines(h)).FirstOrDefault();
        var feats = t?.Rows.Where(r => r.Count >= 4).Select(r => new Feature(r[0], r[1], r[2], r[3])).ToList() ?? new();
        return new Roadmap { File = md, HasFeaturesSection = true, Header = t?.Header, Features = feats };
    }

    private List<Area> LoadAreas()
    {
        var result = new List<Area>();
        foreach (var dir in EnumerateDirs("docs/areas").Where(d => d.Count(c => c == '/') == 2))
        {
            var folder = dir["docs/areas/".Length..];
            var area = new Area { Folder = folder, Registry = Registries.Areas.FirstOrDefault(r => r.Folder == folder) };
            area.Index = LoadMd($"{dir}/userstories.md");
            area.Suite = LoadMd($"{dir}/testsuite.md");
            area.Operations = LoadMd($"{dir}/operations.md");
            foreach (var f in EnumerateFiles($"{dir}/user-stories").Where(f => f.EndsWith(".md", StringComparison.Ordinal) && f.Count(c => c == '/') == 4))
                area.Stories.Add(new StoryFile(area, f, ReadText(f)!));
            foreach (var f in EnumerateFiles($"{dir}/test-cases").Where(f => f.EndsWith(".md", StringComparison.Ordinal) && f.Count(c => c == '/') == 4))
                area.TestCases.Add(new TestCaseFile(area, f, ReadText(f)!));
            result.Add(area);
        }
        return result;
    }

    private List<TestClassInfo> LoadTestClasses()
    {
        var result = new List<TestClassInfo>();
        foreach (var f in EnumerateFiles("tests").Where(f => f.EndsWith(".cs", StringComparison.Ordinal) && !f.Contains("/Fixtures/", StringComparison.Ordinal)))
        {
            var level = LevelOf(f);
            if (level is null) continue;
            var cs = CSharpScanner.Parse(f, ReadText(f)!);
            foreach (var c in cs.Classes.Where(c => c.IsTestClass || (level == TestLevel.Capture && !c.IsAbstract && !c.IsStatic && c.ClassAttributes.Count > 0)))
                result.Add(new TestClassInfo { Path = f, Level = level.Value, Class = c });
        }
        return result;
    }

    /// <summary>All classes under tests/e2e/**/Manual/ (capture classes), tests or not.</summary>
    public static TestLevel? LevelOf(string path)
    {
        if (path.StartsWith("tests/e2e/", StringComparison.Ordinal))
            return path.Split('/').Skip(3).SkipLast(1).Contains("Manual") ? TestLevel.Capture : TestLevel.E2E;
        if (path.StartsWith("tests/integration/", StringComparison.Ordinal)) return TestLevel.Integration;
        if (path.StartsWith("tests/unit/", StringComparison.Ordinal)) return TestLevel.Unit;
        if (path.StartsWith("tests/support/", StringComparison.Ordinal)) return TestLevel.Monitor;
        return null;
    }

    /// <summary>Files whose story ids are test data, not references to real stories (development.md 11.2).</summary>
    public static bool HoldsStoryIdsAsTestData(string path) =>
        path.StartsWith("tests/support/", StringComparison.Ordinal) ||
        path.StartsWith("tests/unit/MyApp.UnitTests/Rules/", StringComparison.Ordinal) ||
        path.StartsWith("src/frontend/scripts/", StringComparison.Ordinal) ||
        path.Contains("/Fixtures/", StringComparison.Ordinal) || path.Contains("/fixtures/", StringComparison.Ordinal) ||
        path.EndsWith(".test.ts", StringComparison.Ordinal) || path.EndsWith(".test.tsx", StringComparison.Ordinal);

    /// <summary>The rule ids of the table in section 12.2 of a development.md text.</summary>
    public static HashSet<string> RuleIdsIn(string developmentMd)
    {
        var doc = MarkdownDocument.Parse(developmentMd);
        var table = doc.FindStartingWith("12.2 ") is { } h ? MarkdownDocument.ParseTables(doc.SectionLines(h)).FirstOrDefault() : null;
        return table?.Rows.Where(r => r.Count > 0 && Regex.IsMatch(r[0], @"^R\d+$")).Select(r => r[0]).ToHashSet() ?? new();
    }

    private List<CsFile> LoadBackend() =>
        EnumerateFiles("src/backend").Where(f => f.EndsWith(".cs", StringComparison.Ordinal))
            .Select(f => CSharpScanner.Parse(f, ReadText(f)!)).ToList();
}
