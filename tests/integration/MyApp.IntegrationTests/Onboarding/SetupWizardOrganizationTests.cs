using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Onboarding;

/// <summary>
/// The wizard's last step creates the organization and makes the person its Admin. Moved from the old
/// OrgCreate001Tests and OrgCreate002Tests, which created organizations with a seed user and removed them
/// afterwards; an organization can no longer be deleted, so a new user creates it.
/// </summary>
[TestFixture]
[Category("onboarding")]
[Property("UserStory", "US-ONBOARD-02")]
public class SetupWizardOrganizationTests
{
    [Test]
    [Description("Creating the organization returns it with every field and stores it")]
    public async Task Create_ReturnsAndStoresTheOrganization()
    {
        var person = await TestWorld.CreateUser("Wizard", completedOnboarding: false);
        var name = $"Wizard Org {Guid.NewGuid():N}"[..24];

        var response = await Api.Post("/api/organizations", TestWorld.OrganizationBody(name), person.Id);

        ApiAssert.IsSuccess(response, 201);
        var id = response.String("id");
        Assert.That(Guid.TryParse(id, out _), Is.True, "id is a GUID");
        Assert.That(response.String("name"), Is.EqualTo(name));
        Assert.That(response.String("legalName"), Is.EqualTo("Integration Test S.r.l."));
        Assert.That(response.String("vatNumber"), Is.EqualTo("IT00000000001"));
        Assert.That(response.String("fiscalCode"), Is.EqualTo("00000000001"));
        Assert.That(response.String("billingAddress"), Is.EqualTo("Via Test 1"));
        Assert.That(response.String("billingCity"), Is.EqualTo("Milano"));
        Assert.That(response.String("billingProvince"), Is.EqualTo("MI"));
        Assert.That(response.String("billingZipCode"), Is.EqualTo("20100"));
        Assert.That(response.String("billingCountryCode"), Is.EqualTo("IT"));
        Assert.That(response.String("billingEmail"), Is.EqualTo("billing@myapp.test"));

        var stored = await Api.Get($"/api/organizations/{id}", person.Id);
        ApiAssert.IsSuccess(stored);
        Assert.That(stored.String("name"), Is.EqualTo(name));
    }

    [Test]
    [Description("The person who creates the organization is its only member, as an active Admin, and signs in to it")]
    public async Task Creator_IsTheActiveAdmin()
    {
        var person = await TestWorld.CreateUser("Creator", completedOnboarding: false);
        var created = await Api.Post("/api/organizations", TestWorld.OrganizationBody(), person.Id);
        ApiAssert.IsSuccess(created, 201);
        var id = created.String("id");

        var team = await Api.Get($"/api/organizations/{id}/users", person.Id);

        ApiAssert.IsSuccess(team);
        var members = team.Json.EnumerateArray().ToList();
        Assert.That(members, Has.Count.EqualTo(1), "The creator is the only member");
        Assert.That(members[0].GetProperty("role").GetString(), Is.EqualTo("Admin"));
        Assert.That(members[0].GetProperty("status").GetString(), Is.EqualTo("Active"));
        Assert.That(members[0].GetProperty("email").GetString(), Is.EqualTo(person.Email));

        var sync = await Api.Post("/api/auth/sync", null, person.Id);
        var joined = sync.Json.GetProperty("organizations").EnumerateArray().Single();
        Assert.That(joined.GetProperty("organizationId").GetString(), Is.EqualTo(id));
        Assert.That(joined.GetProperty("role").GetString(), Is.EqualTo("Admin"));
    }
}
