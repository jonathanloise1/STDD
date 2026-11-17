using MyApp.Infrastructure.Seed;
using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Audit;

/// <summary>
/// The keyed outcome of the audit trail (operations.md 4.2), reached through the fault switch.
/// </summary>
[TestFixture]
[Category("audit")]
[Property("UserStory", "US-AUD-01")]
[Property("TestCase", "AUD-LIST-007")]
public class AuditOutcomeCodesTests
{
    [Test]
    public async Task aud_list_unavailable()
    {
        var response = await Api.Get($"/api/organizations/{SeedDataSet.DevOrg}/audit-logs", SeedDataSet.DevUser, Api.Fault("aud.list.unavailable"));

        ApiAssert.IsProblem(response, 503, "aud.list.unavailable", "The service did not answer. Try again in a few minutes.");
    }
}
