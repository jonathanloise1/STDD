using MyApp.Infrastructure.Seed;
using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Organizations;

/// <summary>
/// Seeing the organizations of the signed-in user and the details of one. Moved from the old OrgList001Tests.
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("UserStory", "US-ORG-01")]
public class OrganizationDetailsTests
{
    [Test]
    [Description("The list holds the organizations of the user, each with its names and no members")]
    public async Task List_HoldsTheOrganizationsOfTheUser()
    {
        var response = await Api.Get("/api/organizations", SeedDataSet.DevUser);

        ApiAssert.IsSuccess(response);
        var organizations = response.Json.EnumerateArray().ToList();
        Assert.That(organizations.Any(o => o.GetProperty("name").GetString() == "MyApp Dev Org"), Is.True);
        foreach (var organization in organizations)
        {
            Assert.That(Guid.TryParse(organization.GetProperty("id").GetString(), out _), Is.True, "id is a GUID");
            Assert.That(organization.GetProperty("name").GetString(), Is.Not.Empty);
            Assert.That(organization.GetProperty("legalName").GetString(), Is.Not.Empty);
            Assert.That(organization.TryGetProperty("users", out _), Is.False, "the list shows no members, only an Admin reads them");
        }
    }

    [Test]
    [Description("A user who belongs to no organization gets an empty list")]
    public async Task List_ForAUserWithNoOrganization_IsEmpty()
    {
        var person = await TestWorld.CreateUser();

        var response = await Api.Get("/api/organizations", person.Id);

        ApiAssert.IsSuccess(response);
        Assert.That(response.Json.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    [Description("An Admin reads the details of the organization")]
    public async Task Details_AreShownToAnAdmin()
    {
        var response = await Api.Get($"/api/organizations/{SeedDataSet.DevOrg}", SeedDataSet.DevUser);

        ApiAssert.IsSuccess(response);
        Assert.That(response.String("name"), Is.EqualTo("MyApp Dev Org"));
    }

    [TestCase("Editor")]
    [TestCase("Viewer")]
    [Description("A member who is not an Admin is refused the details, which hold the billing data")]
    public async Task Details_AreRefusedToAMemberWhoIsNotAnAdmin(string role)
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var member = await TestWorld.AddMember(organizationId, admin, role);

        var response = await Api.Get($"/api/organizations/{organizationId}", member.Id);

        ApiAssert.IsProblem(response, 403);
    }

    [Test]
    [Description("A removed member no longer sees the organization in the list")]
    public async Task List_NoLongerHoldsTheOrganizationOfARemovedMember()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var member = await TestWorld.AddMember(organizationId, admin, "Viewer");
        ApiAssert.IsSuccess(await Api.Get("/api/organizations", member.Id));

        ApiAssert.IsSuccess(await Api.Delete($"/api/organizations/{organizationId}/collaborators/{member.RowId}", admin.Id), 204);

        var response = await Api.Get("/api/organizations", member.Id);
        ApiAssert.IsSuccess(response);
        Assert.That(response.Json.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    [Description("A user who is not a member is refused the details")]
    public async Task Details_AreRefusedToANonMember()
    {
        var response = await Api.Get($"/api/organizations/{SeedDataSet.DevOrg}", SeedDataSet.EmptyAdmin);

        ApiAssert.IsProblem(response, 403);
    }
}
