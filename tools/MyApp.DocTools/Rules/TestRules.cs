using System.Text.RegularExpressions;
using MyApp.DocTools.Model;

namespace MyApp.DocTools.Rules;

internal static class TestScan
{
    private static readonly Regex PageObject = new(@"\b(?!IPage\b)[A-Z]\w*Page\b", RegexOptions.Compiled);
    public static bool UsesPageObject(TestClassInfo c) => PageObject.IsMatch(c.Class.MaskedBody);

    public static TestCaseFile? FindTestCase(Repo repo, string id) => repo.AllTestCases.FirstOrDefault(t => t.Stem == id);
}

/// <summary>R16: test tags resolve to files; no tags on methods.</summary>
public sealed class R16TestTagsResolve : Rule
{
    public override string Id => "R16";
    public override string Name => "Test tags resolve";

    public override IEnumerable<Finding> Check(Repo repo)
    {
        var storyIds = repo.AllStories.Select(s => s.Stem).ToHashSet();
        var caseIds = repo.AllTestCases.Select(t => t.Stem).ToHashSet();
        var folders = repo.AreaFolders;
        foreach (var c in repo.TestClasses)
        {
            foreach (var tag in c.Class.TestCaseTags.Where(x => !caseIds.Contains(x)))
                yield return Found(c.Path, $"{c.Name}: the TestCase tag \"{tag}\" names no test case file.");
            foreach (var tag in c.Class.UserStoryTags.Where(x => !storyIds.Contains(x)))
                yield return Found(c.Path, $"{c.Name}: the UserStory tag \"{tag}\" names no story file.");
            foreach (var tag in c.Class.MethodTags.Distinct())
                yield return Found(c.Path, $"{c.Name}: tag {tag} is written on a method; tags belong on the class only (section 11.1).");
            foreach (var cat in c.Class.MethodCategories.Where(folders.Contains).Distinct())
                yield return Found(c.Path, $"{c.Name}: Category \"{cat}\" names an area folder, so it is a tag, and it is written on a method; tags belong on the class only (section 11.1).");
            if (c.Level == TestLevel.E2E)
                foreach (var tc in c.Class.TestCaseTags.Select(id => TestScan.FindTestCase(repo, id)).Where(t => t?.UserStory is not null))
                    foreach (var us in c.Class.UserStoryTags.Where(u => u != tc!.UserStory))
                        yield return Found(c.Path, $"{c.Name}: UserStory tag \"{us}\" differs from the User Story \"{tc!.UserStory}\" of test case {tc.Stem}.");
        }
    }
}

/// <summary>R17: tag multiplicity per level and E2E naming and location. Categories that name no area folder are selection labels, except that manual-capture is only for capture classes.</summary>
public sealed class R17TagMultiplicityAndNaming : Rule
{
    public override string Id => "R17";
    public override string Name => "Tag multiplicity and naming";

    private static readonly Regex E2EPath = new(@"^tests/e2e/[^/]+/Tests/(?<area>[^/]+)/(?<file>[^/]+)\.cs$", RegexOptions.Compiled);

    public override IEnumerable<Finding> Check(Repo repo)
    {
        var folders = repo.AreaFolders;
        var e2eByCase = new Dictionary<string, List<string>>();
        foreach (var c in repo.TestClasses.Where(c => c.Level == TestLevel.E2E))
            foreach (var id in c.Class.TestCaseTags) { if (!e2eByCase.TryGetValue(id, out var l)) e2eByCase[id] = l = new(); l.Add(c.Name); }

        foreach (var c in repo.TestClasses)
        {
            var us = c.Class.UserStoryTags.Count; var tcs = c.Class.TestCaseTags.Count; var cats = c.Class.Categories;
            var areaCats = cats.Where(folders.Contains).ToList();
            var n = c.Name;
            switch (c.Level)
            {
                case TestLevel.Unit:
                    if (tcs > 0) yield return Found(c.Path, $"{n}: unit tests must not carry a TestCase tag.");
                    break;
                case TestLevel.Integration:
                    if (us < 1) yield return Found(c.Path, $"{n}: integration tests need at least one UserStory tag.");
                    if (areaCats.Count < 1) yield return Found(c.Path, $"{n}: integration tests need a Category tag with the area folder{Seen(cats)}.");
                    break;
                case TestLevel.E2E:
                    if (us != 1) yield return Found(c.Path, $"{n}: E2E tests need exactly one UserStory tag; found {us}.");
                    if (tcs != 1) yield return Found(c.Path, $"{n}: E2E tests need exactly one TestCase tag; found {tcs}.");
                    if (areaCats.Count != 1) yield return Found(c.Path, $"{n}: E2E tests need exactly one Category tag with the area folder{Seen(cats)}.");
                    if (tcs == 1)
                    {
                        var id = c.Class.TestCaseTags[0];
                        var expectedName = Ids.TestCasePascal(id) + "Tests";
                        if (n != expectedName) yield return Found(c.Path, $"{n}: the class name for test case {id} must be {expectedName} (section 10.2).");
                        var area = TestScan.FindTestCase(repo, id)?.Area.Folder;
                        if (area is not null && areaCats.Count == 1 && areaCats[0] != area)
                            yield return Found(c.Path, $"{n}: test case {id} belongs to the area \"{area}\", so its Category tag must be \"{area}\", not \"{areaCats[0]}\".");
                        var m = E2EPath.Match(c.Path);
                        if (!m.Success) yield return Found(c.Path, $"{n}: E2E tests live in tests/e2e/{{project}}/Tests/{{AreaPascal}}/{{ClassName}}.cs.");
                        else
                        {
                            if (m.Groups["file"].Value != n) yield return Found(c.Path, $"{n}: the file name must equal the class name.");
                            if (area is not null && m.Groups["area"].Value != Ids.AreaPascal(area))
                                yield return Found(c.Path, $"{n}: test case {id} belongs to the area \"{area}\", so the folder must be {Ids.AreaPascal(area)}.");
                        }
                        if (e2eByCase[id].Count > 1) yield return Found(c.Path, $"{n}: test case {id} is tagged on {e2eByCase[id].Count} E2E classes ({string.Join(", ", e2eByCase[id])}); there is one class per test case.");
                    }
                    break;
                case TestLevel.Capture:
                    if (us < 1) yield return Found(c.Path, $"{n}: capture classes need at least one UserStory tag.");
                    if (tcs > 0) yield return Found(c.Path, $"{n}: capture classes must not carry a TestCase tag.");
                    if (!cats.Contains("manual-capture")) yield return Found(c.Path, $"{n}: capture classes need the Category \"manual-capture\".");
                    if (areaCats.Count < 1) yield return Found(c.Path, $"{n}: capture classes need a Category tag with the area folder{Seen(cats)}.");
                    break;
                case TestLevel.Monitor:
                    if (us + tcs + areaCats.Count > 0) yield return Found(c.Path, $"{n}: monitor tests serve no story, so they carry no UserStory, TestCase or area Category tag (section 11.1).");
                    break;
            }
            if (c.Level != TestLevel.Capture && cats.Contains("manual-capture"))
                yield return Found(c.Path, $"{n}: Category \"manual-capture\" is only for capture classes under Manual/; it takes a test out of the normal run (section 10.4).");
        }
    }

    private static string Seen(List<string> cats) =>
        cats.Count == 0 ? "" : $" (found {string.Join(", ", cats.Select(x => $"\"{x}\""))})";
}

/// <summary>R18: Automated by equals the value derived from the tags.</summary>
public sealed class R18AutomatedByDerived : Rule
{
    public override string Id => "R18";
    public override string Name => "Automated by derived";

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var t in repo.AllTestCases)
        {
            var derived = AutomatedBy.Derive(repo, t.Stem);
            var actual = t.AutomatedBy;
            if (actual is null) yield return Found(t.Path, $"The Automated by field is missing; the derived value is \"{derived}\".");
            else if (actual != derived) yield return Found(t.Path, $"Automated by is \"{actual}\" but the tags derive \"{derived}\". Run: dotnet run --project tools/MyApp.DocTools -- sync-automated-by");
        }
    }
}

/// <summary>R19: test cases of Done features are automated by at least one E2E test.</summary>
public sealed class R19DoneFeaturesAutomated : Rule
{
    public override string Id => "R19";
    public override string Name => "Done features automated";

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var area in repo.Areas)
            foreach (var t in area.TestCases)
            {
                var story = area.Stories.FirstOrDefault(s => s.Stem == t.UserStory);
                var feature = story?.Feature is { } f ? repo.Roadmap.Find(f) : null;
                if (feature is not { Status: "Done" }) continue;
                var items = (t.AutomatedBy ?? "").Split(',').Select(x => x.Trim());
                if (!items.Any(x => x.StartsWith("e2e/", StringComparison.Ordinal)))
                    yield return Found(t.Path, $"Feature {feature.Id} is Done but this test case has no e2e/ item in Automated by. Write the E2E test.");
            }
    }
}

/// <summary>R20: E2E test classes use page objects and no API, database or direct locators.</summary>
public sealed class R20E2EDrivesTheUi : Rule
{
    public override string Id => "R20";
    public override string Name => "E2E drives the UI";

    private static readonly (string Label, Regex Rx)[] Forbidden =
    {
        ("the API (APIRequest)", new Regex(@"\bAPIRequest\w*")),
        ("the API (HttpClient)", new Regex(@"\bHttpClient\b")),
        ("the database", new Regex(@"\b(DbContext|DbConnection|SqlConnection|NpgsqlConnection|SqliteConnection|IDbConnection)\b")),
        ("a locator method", new Regex(@"\.\s*(Locator|GetByRole|GetByText|GetByLabel|GetByTestId|GetByPlaceholder|GetByAltText|GetByTitle|FrameLocator)\s*\(")),
    };

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var c in repo.TestClasses.Where(c => c.Level == TestLevel.E2E))
        {
            if (!TestScan.UsesPageObject(c)) yield return Found(c.Path, $"{c.Name}: the test uses no page object (a class ending in Page from PageObjects/).");
            foreach (var (label, rx) in Forbidden)
                if (rx.Match(c.Class.MaskedBody) is { Success: true } m)
                    yield return Found(c.Path, $"{c.Name}: E2E tests must not use {label} (found \"{m.Value.Trim()}\"); move it into a page object or an integration test.");
        }
    }
}

/// <summary>R21: capture classes use page objects, have no TestCase tag, and the manual-capture category.</summary>
public sealed class R21CaptureClasses : Rule
{
    public override string Id => "R21";
    public override string Name => "Capture classes";

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var c in repo.TestClasses.Where(c => c.Level == TestLevel.Capture))
        {
            if (!TestScan.UsesPageObject(c)) yield return Found(c.Path, $"{c.Name}: a capture class must use at least one page object.");
            if (c.Class.TestCaseTags.Count > 0) yield return Found(c.Path, $"{c.Name}: a capture class must not have a TestCase tag.");
            if (!c.Class.Categories.Contains("manual-capture")) yield return Found(c.Path, $"{c.Name}: a capture class needs [Category(\"manual-capture\")].");
        }
    }
}
