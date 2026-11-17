using MyApp.DocTools.Model;

namespace MyApp.DocTools.Rules;

/// <summary>A traceability rule of development.md section 12.2.</summary>
public abstract class Rule
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract IEnumerable<Finding> Check(Repo repo);

    protected Finding Found(string subject, string message) => new(Id, subject, message);
}

public static class RuleSet
{
    public static IReadOnlyList<Rule> All { get; } = new Rule[]
    {
        new R01AreaRegistry(), new R02AllowedFiles(), new R03Encoding(), new R04RequiredHeadings(), new R05IdsAndFileNames(),
        new R06FieldsAndVocabularies(), new R07StorySize(), new R08BannedContent(), new R09IndexesComplete(),
        new R10TestCaseToStory(), new R11Coverage(), new R12ErrorStatesCovered(), new R13ObservableTraceComplete(),
        new R14RunbookExists(), new R15LogKeyEmitted(), new R16TestTagsResolve(), new R17TagMultiplicityAndNaming(),
        new R18AutomatedByDerived(), new R19DoneFeaturesAutomated(), new R20E2EDrivesTheUi(), new R21CaptureClasses(),
        new R22StoryIdsInCode(), new R23ContractVersions(),
    };

    public static Rule Get(string id) => All.First(r => r.Id == id);

    public static List<Finding> Run(Repo repo, IEnumerable<Rule>? rules = null) =>
        (rules ?? All).SelectMany(r => r.Check(repo))
            .Distinct()
            .OrderBy(f => f.Rule, StringComparer.Ordinal).ThenBy(f => f.Subject, StringComparer.Ordinal).ThenBy(f => f.Message, StringComparer.Ordinal)
            .ToList();
}

/// <summary>Heading templates of sections 5, 6, 8 and 9, shared by R04 and R08.</summary>
public static class Templates
{
    public static readonly string[] IndexH2 = { "Overview", "Stories", "Business Rules", "UI Flow", "Access Control", "Open Questions", "Files" };
    public static readonly string[] StoryH2 = { "Story", "Acceptance Criteria", "Access Control", "Business Rules", "Error States", "Non-Goals", "Questions users ask" };
    public static readonly string[] CaseH2 = { "Description", "Preconditions", "Test Data", "Steps", "Expected Results" };
    public const string TraceH2 = "Observable trace";
    public static readonly string[] SuiteH2 = { "Coverage Matrix", "Seed Data" };

    /// <summary>Expected (level, text) sequence after the H1.</summary>
    public static List<(int Level, string Text)> StoryHeadings()
    {
        var l = new List<(int, string)>();
        foreach (var h in StoryH2)
        {
            l.Add((2, h));
            if (h == "Questions users ask") { l.Add((3, "en")); l.Add((3, "it")); }
        }
        return l;
    }

    public static List<(int Level, string Text)> Flat(IEnumerable<string> h2) => h2.Select(h => (2, h)).ToList();
}
