using MyApp.DocTools;
using MyApp.UnitTests.Fixtures;
using NUnit.Framework;

namespace MyApp.UnitTests.Rules;

[TestFixture]
public class SyncAutomatedByTests
{
    private static string Tc(string n) => FixtureRepo.TestCasePath("TASK-CREATE-" + n);

    [Test]
    public void Derive_ListsE2EFirstThenIntegration()
    {
        using var fx = new FixtureRepo();
        Assert.That(AutomatedBy.Derive(fx.Open(), "TASK-CREATE-005"), Is.EqualTo("e2e/TaskCreate005Tests, integration/TaskCreateApiTests"));
        Assert.That(AutomatedBy.Derive(fx.Open(), "TASK-CREATE-001"), Is.EqualTo("e2e/TaskCreate001Tests"));
        Assert.That(AutomatedBy.Derive(fx.Open(), "TASK-CREATE-099"), Is.EqualTo("None"));
    }

    [Test]
    public void Sync_RewritesWrongValuesAndLeavesTheRestUntouched()
    {
        using var fx = new FixtureRepo();
        var before = fx.Read(Tc("005"));
        fx.Edit(Tc("005"), "| Automated by | e2e/TaskCreate005Tests, integration/TaskCreateApiTests |", "| Automated by | None |");
        fx.Edit(Tc("001"), "| Automated by | e2e/TaskCreate001Tests |", "| Automated by | whatever by hand |");
        var changed = AutomatedBy.Sync(fx.Open());
        Assert.That(changed, Is.EquivalentTo(new[] { Tc("005"), Tc("001") }));
        Assert.That(fx.Read(Tc("005")), Is.EqualTo(before));
        StringAssert.Contains("| Automated by | e2e/TaskCreate001Tests |", fx.Read(Tc("001")));
    }

    [Test]
    public void Sync_IsIdempotentAndMakesR18Pass()
    {
        using var fx = new FixtureRepo();
        fx.Edit(Tc("003"), "| Automated by | e2e/TaskCreate003Tests |", "| Automated by | None |");
        Assert.That(fx.Run("R18"), Is.Not.Empty);
        AutomatedBy.Sync(fx.Open());
        Assert.That(fx.Run("R18"), Is.Empty);
        Assert.That(AutomatedBy.Sync(fx.Open()), Is.Empty);
    }

    [Test]
    public void Sync_WritesNoneWhenNoTestCarriesTheTag()
    {
        using var fx = new FixtureRepo();
        fx.Delete(FixtureRepo.E2EPath("TASK-CREATE-004"));
        AutomatedBy.Sync(fx.Open());
        StringAssert.Contains("| Automated by | None |", fx.Read(Tc("004")));
    }

    [Test]
    public void Sync_PreservesCrLfFiles()
    {
        using var fx = new FixtureRepo();
        var text = fx.Read(Tc("001")).Replace("e2e/TaskCreate001Tests", "None").Replace("\n", "\r\n");
        fx.WriteBytes(Tc("001"), System.Text.Encoding.UTF8.GetBytes(text));
        AutomatedBy.Sync(fx.Open());
        var after = File.ReadAllText(fx.Abs(Tc("001")));
        StringAssert.Contains("| Automated by | e2e/TaskCreate001Tests |\r\n", after);
    }

    [Test]
    public void Sync_IgnoresUnitAndCaptureClasses()
    {
        using var fx = new FixtureRepo();
        fx.Write("tests/e2e/MyApp.E2E/Manual/Tasks/Odd.cs", "using NUnit.Framework;\n[TestFixture]\n[Property(\"TestCase\", \"TASK-CREATE-001\")]\npublic class OddCapture { [Test] public void T() { } }\n");
        Assert.That(AutomatedBy.Derive(fx.Open(), "TASK-CREATE-001"), Is.EqualTo("e2e/TaskCreate001Tests"));
    }
}
