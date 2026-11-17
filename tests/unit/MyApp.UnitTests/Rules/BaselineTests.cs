using MyApp.DocTools;
using MyApp.UnitTests.Fixtures;
using NUnit.Framework;

namespace MyApp.UnitTests.Rules;

[TestFixture]
public class BaselineTests
{
    private static Finding F(string rule, string subject) => new(rule, subject, "message long enough");
    private static Baseline B(params (string, string)[] e) => new() { Entries = e.Select(x => new BaselineEntry(x.Item1, x.Item2)).ToList() };

    [Test]
    public void ListedFinding_IsKnownAndDoesNotFail()
    {
        var r = Baseline.Compare(new[] { F("R14", "a.md") }, B(("R14", "a.md")));
        Assert.That(r.Known, Has.Count.EqualTo(1));
        Assert.That(r.New, Is.Empty);
        Assert.That(r.Stale, Is.Empty);
    }

    [Test]
    public void UnlistedFinding_IsNewAndFails()
    {
        var r = Baseline.Compare(new[] { F("R14", "a.md"), F("R14", "b.md") }, B(("R14", "a.md")));
        Assert.That(r.New.Select(f => f.Subject), Is.EqualTo(new[] { "b.md" }));
    }

    [Test]
    public void SameSubjectUnderAnotherRule_IsNotCoveredByTheEntry()
    {
        var r = Baseline.Compare(new[] { F("R08", "a.md") }, B(("R14", "a.md")));
        Assert.That(r.New, Has.Count.EqualTo(1));
        Assert.That(r.Stale, Has.Count.EqualTo(1));
    }

    [Test]
    public void EntryWithoutFinding_IsStale()
    {
        var r = Baseline.Compare(Array.Empty<Finding>(), B(("R14", "a.md")));
        Assert.That(r.Stale, Is.EqualTo(new[] { new BaselineEntry("R14", "a.md") }));
    }

    [Test]
    public void Problems_ReportUnsortedAndDuplicateEntries()
    {
        Assert.That(B(("R02", "a"), ("R01", "b")).Problems(), Is.Not.Empty);
        Assert.That(B(("R01", "a"), ("R01", "a")).Problems(), Is.Not.Empty);
        Assert.That(B(("R01", "a"), ("R01", "b"), ("R02", "a")).Problems(), Is.Empty);
    }

    [Test]
    public void AddedSince_ReportsEntriesTheTargetDoesNotHave()
    {
        var added = Baseline.AddedSince(B(("R14", "a.md"), ("R14", "b.md")), B(("R14", "a.md")), new HashSet<string> { "R14" });
        Assert.That(added, Is.EqualTo(new[] { new BaselineEntry("R14", "b.md") }));
    }

    [Test]
    public void AddedSince_AcceptsEntriesOfARuleTheTargetDoesNotHaveYet()
    {
        Assert.That(Baseline.AddedSince(B(("R24", "a.md")), B(), new HashSet<string> { "R14" }), Is.Empty);
    }

    [Test]
    public void AddedSince_AcceptsAShrinkingBaseline()
    {
        Assert.That(Baseline.AddedSince(B(("R14", "a.md")), B(("R14", "a.md"), ("R14", "b.md")), new HashSet<string> { "R14" }), Is.Empty);
    }

    [Test]
    public void RuleIdsIn_ReadsTheTableOfSection12_2()
    {
        var ids = MyApp.DocTools.Model.Repo.RuleIdsIn("## 12. Rules\n\n### 12.2 Rules\n\n| Id | Name |\n|----|------|\n| R01 | A |\n| R02 | B |\n\n### 12.3 Baseline\n\n| Rule | Effect |\n|------|--------|\n| R99 | C |\n");
        Assert.That(ids, Is.EquivalentTo(new[] { "R01", "R02" }));
    }

    [Test]
    public void Serialize_SortsDeduplicatesAndRoundTrips()
    {
        var json = Baseline.Serialize(new[] { F("R14", "b.md"), F("R02", "z.md"), F("R14", "a.md"), F("R14", "a.md") });
        var path = Path.Combine(Path.GetTempPath(), "baseline-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, json);
            var loaded = Baseline.Load(path);
            Assert.That(loaded.Entries.Select(e => $"{e.Rule} {e.Subject}"), Is.EqualTo(new[] { "R02 z.md", "R14 a.md", "R14 b.md" }));
            Assert.That(loaded.Problems(), Is.Empty);
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void Serialize_EmptyIsTheShippedForm() => Assert.That(Baseline.Serialize(Array.Empty<Finding>()).Trim(), Is.EqualTo("{ \"findings\": [] }"));

    [Test]
    public void ShippedBaselineIsEmptyAndValid()
    {
        var root = MyApp.DocTools.Model.Repo.FindRoot(AppContext.BaseDirectory)!;
        var b = Baseline.Load(Path.Combine(root, MyApp.DocTools.Model.Repo.BaselinePath));
        Assert.That(b.Problems(), Is.Empty);
    }
}

[TestFixture]
public class CliTests
{
    private static (int Code, string Out, string Err) Run(params string[] args) => RunWith(null, args);

    private static (int Code, string Out, string Err) RunWith(IGitReader? git, params string[] args)
    {
        var o = new StringWriter(); var e = new StringWriter();
        var code = Program.Run(args, o, e, git);
        return (code, o.ToString(), e.ToString());
    }

    private sealed class FakeGit(string? baseline, string? development = "### 12.2 Rules\n\n| Id | Name |\n|----|------|\n| R02 | Allowed files |\n", bool exists = true) : IGitReader
    {
        public bool RefExists(string gitRef) => exists;
        public string? ReadFile(string gitRef, string path) =>
            path == MyApp.DocTools.Model.Repo.BaselinePath ? baseline : path == MyApp.DocTools.Model.Repo.DevelopmentPath ? development : null;
    }

    private const string BaselineFile = "tests/unit/MyApp.UnitTests/Architecture/baseline.json";
    private const string Empty = "{ \"findings\": [] }\n";
    private const string WithNotes = "{ \"findings\": [ { \"rule\": \"R02\", \"subject\": \"docs/areas/tasks/notes.txt\" } ] }\n";

    private static FixtureRepo RepoWithBaselinedFinding()
    {
        var fx = new FixtureRepo();
        fx.Write("docs/areas/tasks/notes.txt", "x");
        fx.Write(BaselineFile, WithNotes);
        return fx;
    }

    [Test]
    public void TargetRef_EntryTheTargetDoesNotHave_ExitsNonZero()
    {
        using var fx = RepoWithBaselinedFinding();
        var r = RunWith(new FakeGit(Empty), "check", fx.Root, "--target-ref", "origin/main");
        Assert.That(r.Code, Is.EqualTo(1), r.Out + r.Err);
        StringAssert.Contains("ADDED R02 docs/areas/tasks/notes.txt", r.Out);
    }

    [Test]
    public void TargetRef_EntryTheTargetHas_ExitsZero()
    {
        using var fx = RepoWithBaselinedFinding();
        var r = RunWith(new FakeGit(WithNotes), "check", fx.Root, "--target-ref", "origin/main");
        Assert.That(r.Code, Is.EqualTo(0), r.Out + r.Err);
        StringAssert.Contains("0 added since origin/main", r.Out);
    }

    [Test]
    public void TargetRef_RuleIntroducedInThisChange_ExitsZero()
    {
        using var fx = RepoWithBaselinedFinding();
        var r = RunWith(new FakeGit(Empty, "### 12.2 Rules\n\n| Id | Name |\n|----|------|\n| R01 | Area registry |\n"), "check", fx.Root, "--target-ref", "origin/main");
        Assert.That(r.Code, Is.EqualTo(0), r.Out + r.Err);
    }

    [Test]
    public void TargetRef_NoBaselineAtTheTarget_IsFirstAdoption()
    {
        using var fx = RepoWithBaselinedFinding();
        var r = RunWith(new FakeGit(null), "check", fx.Root, "--target-ref", "origin/main");
        Assert.That(r.Code, Is.EqualTo(0), r.Out + r.Err);
        StringAssert.Contains("first adoption", r.Out);
    }

    [Test]
    public void TargetRef_UnreadableRef_ExitsWithUsageError()
    {
        using var fx = RepoWithBaselinedFinding();
        var r = RunWith(new FakeGit(Empty, exists: false), "check", fx.Root, "--target-ref", "nope");
        Assert.That(r.Code, Is.EqualTo(2));
        StringAssert.Contains("nope", r.Err);
    }

    [Test]
    public void TargetRef_OutsideAGitRepository_ExitsWithUsageError()
    {
        using var fx = RepoWithBaselinedFinding();
        Assert.That(Run("check", fx.Root, "--target-ref", "origin/main").Code, Is.EqualTo(2));
    }

    [Test]
    public void Check_OnValidRepository_ExitsZero()
    {
        using var fx = new FixtureRepo();
        fx.Write("tests/unit/MyApp.UnitTests/Architecture/baseline.json", "{ \"findings\": [] }\n");
        var r = Run("check", fx.Root);
        Assert.That(r.Code, Is.EqualTo(0), r.Out + r.Err);
        StringAssert.Contains("0 new", r.Out);
    }

    [Test]
    public void Check_WithUnlistedFinding_ExitsNonZeroAndGroupsByRule()
    {
        using var fx = new FixtureRepo();
        fx.Write("tests/unit/MyApp.UnitTests/Architecture/baseline.json", "{ \"findings\": [] }\n");
        fx.Write("docs/areas/tasks/notes.txt", "x");
        var r = Run("check", fx.Root);
        Assert.That(r.Code, Is.EqualTo(1));
        StringAssert.Contains("R02 Allowed files", r.Out);
    }

    [Test]
    public void Check_WithListedFinding_ExitsZero()
    {
        using var fx = new FixtureRepo();
        fx.Write("docs/areas/tasks/notes.txt", "x");
        fx.Write("tests/unit/MyApp.UnitTests/Architecture/baseline.json", "{ \"findings\": [ { \"rule\": \"R02\", \"subject\": \"docs/areas/tasks/notes.txt\" } ] }\n");
        var r = Run("check", fx.Root);
        Assert.That(r.Code, Is.EqualTo(0), r.Out);
        StringAssert.Contains("[baselined]", r.Out);
    }

    [Test]
    public void Check_WithStaleEntry_ExitsNonZero()
    {
        using var fx = new FixtureRepo();
        fx.Write("tests/unit/MyApp.UnitTests/Architecture/baseline.json", "{ \"findings\": [ { \"rule\": \"R02\", \"subject\": \"docs/areas/tasks/gone.txt\" } ] }\n");
        var r = Run("check", fx.Root);
        Assert.That(r.Code, Is.EqualTo(1));
        StringAssert.Contains("STALE", r.Out);
    }

    [Test]
    public void WriteBaseline_SaysItIsForFirstAdoptionAndWritesFindings()
    {
        using var fx = new FixtureRepo();
        fx.Write("docs/areas/tasks/notes.txt", "x");
        fx.Write("tests/unit/MyApp.UnitTests/Architecture/baseline.json", "{ \"findings\": [] }\n");
        var r = Run("write-baseline", fx.Root);
        Assert.That(r.Code, Is.EqualTo(0));
        StringAssert.Contains("first adoption only", r.Out);
        StringAssert.Contains("docs/areas/tasks/notes.txt", fx.Read("tests/unit/MyApp.UnitTests/Architecture/baseline.json"));
        Assert.That(Run("check", fx.Root).Code, Is.EqualTo(0));
    }

    [Test]
    public void UnknownCommand_ExitsWithUsageError() => Assert.That(Run("frobnicate").Code, Is.Not.EqualTo(0));
}
