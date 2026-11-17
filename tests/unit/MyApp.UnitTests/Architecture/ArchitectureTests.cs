using MyApp.DocTools;
using MyApp.DocTools.Model;
using MyApp.DocTools.Rules;
using NUnit.Framework;

namespace MyApp.UnitTests.Architecture;

/// <summary>
/// One test per traceability rule (development.md section 12), run against the real repository
/// with the baseline at Architecture/baseline.json. A finding not in the baseline fails; so does a stale entry.
/// </summary>
[TestFixture]
public class ArchitectureTests
{
    private static readonly Lazy<(Repo Repo, Baseline Baseline)> Context = new(() =>
    {
        var root = Repo.FindRoot(AppContext.BaseDirectory) ?? throw new InvalidOperationException("Repository root (docs/contracts.json) not found above " + AppContext.BaseDirectory);
        var repo = new Repo(root);
        return (repo, Baseline.Load(repo.Abs(Repo.BaselinePath)));
    });

    private static void Verify(string ruleId)
    {
        var (repo, baseline) = Context.Value;
        var findings = RuleSet.Get(ruleId).Check(repo).Distinct().ToList();
        var mine = new Baseline { Entries = baseline.Entries.Where(e => e.Rule == ruleId).ToList() };
        var result = Baseline.Compare(findings, mine);
        var lines = result.New.Select(f => $"  {f.Subject}\n      {f.Message}")
            .Concat(result.Stale.Select(s => $"  STALE baseline entry {s.Rule} {s.Subject}: remove it"))
            .ToList();
        Assert.That(lines, Is.Empty, $"{ruleId} {RuleSet.Get(ruleId).Name}:\n" + string.Join("\n", lines));
    }

    [Test] public void R01() => Verify("R01");
    [Test] public void R02() => Verify("R02");
    [Test] public void R03() => Verify("R03");
    [Test] public void R04() => Verify("R04");
    [Test] public void R05() => Verify("R05");
    [Test] public void R06() => Verify("R06");
    [Test] public void R07() => Verify("R07");
    [Test] public void R08() => Verify("R08");
    [Test] public void R09() => Verify("R09");
    [Test] public void R10() => Verify("R10");
    [Test] public void R11() => Verify("R11");
    [Test] public void R12() => Verify("R12");
    [Test] public void R13() => Verify("R13");
    [Test] public void R14() => Verify("R14");
    [Test] public void R15() => Verify("R15");
    [Test] public void R16() => Verify("R16");
    [Test] public void R17() => Verify("R17");
    [Test] public void R18() => Verify("R18");
    [Test] public void R19() => Verify("R19");
    [Test] public void R20() => Verify("R20");
    [Test] public void R21() => Verify("R21");
    [Test] public void R22() => Verify("R22");
    [Test] public void R23() => Verify("R23");

    [Test]
    public void Baseline_IsSortedAndUnique()
    {
        Assert.That(Context.Value.Baseline.Problems(), Is.Empty);
    }

    [Test]
    public void EveryRuleHasATest()
    {
        var methods = typeof(ArchitectureTests).GetMethods().Select(m => m.Name).ToHashSet();
        Assert.That(RuleSet.All.Select(r => r.Id).Where(id => !methods.Contains(id)), Is.Empty);
    }
}
