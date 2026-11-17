using MyApp.DocTools;
using MyApp.DocTools.Rules;
using MyApp.UnitTests.Fixtures;
using NUnit.Framework;

namespace MyApp.UnitTests.Rules;

public abstract class RuleTestBase
{
    protected FixtureRepo Fx = null!;

    [SetUp] public void CreateRepo() => Fx = new FixtureRepo();
    [TearDown] public void DeleteRepo() => Fx.Dispose();

    protected void Passes(string rule) =>
        Assert.That(Fx.Run(rule), Is.Empty, string.Join("\n", Fx.Run(rule).Select(f => f.Subject + ": " + f.Message)));

    /// <summary>The rule reports at least one finding on the subject, and the message is not empty.</summary>
    protected void Fails(string rule, string subject, string? messagePart = null)
    {
        var findings = Fx.Run(rule);
        Assert.That(findings.Select(f => f.Subject), Does.Contain(subject), "findings: " + string.Join(" | ", findings.Select(f => f.Subject + ": " + f.Message)));
        Assert.That(findings.All(f => f.Rule == rule && f.Message.Length > 10));
        if (messagePart is not null)
            Assert.That(findings.Where(f => f.Subject == subject).Select(f => f.Message), Has.Some.Contain(messagePart));
    }
}

[TestFixture]
public class GoldenRepositoryTests : RuleTestBase
{
    [TestCaseSource(nameof(RuleIds))]
    public void EveryRulePassesOnTheValidFixtureRepository(string rule) => Passes(rule);

    [Test]
    public void AllRulesTogetherReportNothing()
    {
        Assert.That(RuleSet.Run(Fx.Open()), Is.Empty);
    }

    [Test]
    public void RuleIdsAreR01ToR23()
    {
        Assert.That(RuleSet.All.Select(r => r.Id), Is.EqualTo(Enumerable.Range(1, 23).Select(i => $"R{i:00}")));
    }

    public static IEnumerable<string> RuleIds => RuleSet.All.Select(r => r.Id);
}
