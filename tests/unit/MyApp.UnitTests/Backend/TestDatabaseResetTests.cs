using Microsoft.Extensions.Configuration;
using MyApp.Infrastructure.Configuration;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

/// <summary>The guard of the E2E database reset: each condition failing on its own means no reset.</summary>
[TestFixture]
public class TestDatabaseResetTests
{
    private const string E2EConnection = "Server=localhost,1433;Database=MyApp_E2E;User Id=u;Password=p;TrustServerCertificate=True;";

    private static IConfiguration Configuration(string? switchValue)
    {
        var values = new Dictionary<string, string?>();
        if (switchValue is not null) values[TestDatabaseReset.SwitchKey] = switchValue;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static TestDatabaseResetDecision Evaluate(string environment, string? switchValue, string? connection) =>
        TestDatabaseReset.Evaluate(Configuration(switchValue), new TestHostEnvironment(environment), connection);

    [Test]
    public void AllConditionsHolding_AllowsTheReset()
    {
        Assert.That(Evaluate("Development", "true", E2EConnection).Allowed, Is.True);
    }

    [TestCase("Production")]
    [TestCase("Staging")]
    [TestCase("Test")]
    public void OutsideDevelopment_NoReset(string environment)
    {
        Assert.That(Evaluate(environment, "true", E2EConnection).Allowed, Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("false")]
    [TestCase("yes")]
    public void WithoutTheSwitch_NoReset(string? switchValue)
    {
        Assert.That(Evaluate("Development", switchValue, E2EConnection).Allowed, Is.False);
        Assert.That(TestDatabaseReset.IsRequested(Configuration(switchValue)), Is.False);
    }

    [TestCase("Server=localhost;Database=MyApp_LOCAL;User Id=u;Password=p;")]
    [TestCase("Server=localhost;Database=MyApp_E2E_Backup;User Id=u;Password=p;")]
    [TestCase("Server=localhost;Database=MyApp_e2e;User Id=u;Password=p;")]
    [TestCase("Server=localhost;Database=_E2E;User Id=u;Password=p;")]
    [TestCase("Server=localhost;Database=master;User Id=u;Password=p;")]
    [TestCase("Server=localhost;User Id=u;Password=p;")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("not a connection string")]
    public void ForAnyOtherDatabase_NoReset(string? connection)
    {
        Assert.That(Evaluate("Development", "true", connection).Allowed, Is.False);
    }

    [TestCase("Server=localhost;Database=MyApp_E2E;User Id=u;Password=p;")]
    [TestCase("Server=localhost;Initial Catalog=MyApp_E2E;User Id=u;Password=p;")]
    [TestCase("Server=localhost;Database=Other_Test;User Id=u;Password=p;")]
    public void ForADatabaseEndingInE2EOrTest_AllowsTheReset(string connection)
    {
        Assert.That(Evaluate("Development", "true", connection).Allowed, Is.True);
    }

    [Test]
    public void TheReasonNamesWhatFailed()
    {
        Assert.That(Evaluate("Production", "true", E2EConnection).Reason, Does.Contain("Development"));
        Assert.That(Evaluate("Development", null, E2EConnection).Reason, Does.Contain(TestDatabaseReset.SwitchKey));
        Assert.That(Evaluate("Development", "true", "Server=x;Database=MyApp_LOCAL;").Reason, Does.Contain("MyApp_LOCAL"));
    }
}
