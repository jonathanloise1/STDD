using MyApp.Infrastructure.Seed;
using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Audit;

/// <summary>
/// The audit trail is read-only and for Admins: entries come newest first with their author and time, and
/// the type filter offers a fixed list of choices.
/// </summary>
[TestFixture]
[Category("audit")]
[Property("UserStory", "US-AUD-01")]
[Property("UserStory", "US-AUD-02")]
public class AuditTrailTests
{
    [Test]
    [Description("An Admin reads the entries of the organization, newest first, with the paging fields")]
    public async Task Admin_ReadsTheEntriesNewestFirst()
    {
        var response = await Api.Get($"/api/organizations/{SeedDataSet.DevOrg}/audit-logs", SeedDataSet.DevUser);

        ApiAssert.IsSuccess(response);
        Assert.That(response.Json.GetProperty("totalCount").GetInt32(), Is.GreaterThan(0));
        Assert.That(response.Json.GetProperty("page").GetInt32(), Is.EqualTo(1));
        var items = response.Json.GetProperty("items").EnumerateArray().ToList();
        Assert.That(items, Is.Not.Empty);
        foreach (var field in new[] { "id", "entityType", "entityTypeName", "entityId", "action", "userId", "userDisplayName", "timestamp" })
            Assert.That(items[0].TryGetProperty(field, out _), Is.True, $"An entry has '{field}'");

        var times = items.Select(i => i.GetProperty("timestamp").GetDateTime()).ToList();
        Assert.That(times, Is.Ordered.Descending);
    }

    [Test]
    [Description("An organization with no history has an empty trail")]
    public async Task Trail_OfAnOrganizationWithNoHistory_IsEmpty()
    {
        var response = await Api.Get($"/api/organizations/{SeedDataSet.EmptyOrg}/audit-logs", SeedDataSet.EmptyAdmin);

        ApiAssert.IsSuccess(response);
        Assert.That(response.Json.GetProperty("totalCount").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    [Description("The type filter has a fixed list of choices, each with a value and a friendly name")]
    public async Task EntityTypes_AreAFixedList()
    {
        var response = await Api.Get($"/api/organizations/{SeedDataSet.DevOrg}/audit-logs/entity-types", SeedDataSet.DevUser);

        ApiAssert.IsSuccess(response);
        var types = response.Json.EnumerateArray().ToList();
        Assert.That(types, Is.Not.Empty);
        foreach (var type in types)
        {
            Assert.That(type.GetProperty("value").GetString(), Is.Not.Empty);
            Assert.That(type.GetProperty("name").GetString(), Is.Not.Empty);
        }
    }

    [TestCase("Editor")]
    [TestCase("Viewer")]
    [Description("Only an Admin reads the audit trail")]
    public async Task NonAdmin_IsRefused(string role)
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var member = await TestWorld.AddMember(organizationId, admin, role);

        var response = await Api.Get($"/api/organizations/{organizationId}/audit-logs", member.Id);

        ApiAssert.IsProblem(response, 403);
    }
}
