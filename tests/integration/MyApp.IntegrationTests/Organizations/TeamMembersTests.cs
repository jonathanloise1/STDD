using MyApp.Infrastructure.Seed;
using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Organizations;

/// <summary>
/// The members and pending invitations of an organization. Moved from the old OrgTeam001Tests, which found
/// "MyApp Dev Org" by name through the API; the seed gives its id.
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("UserStory", "US-ORG-04")]
public class TeamMembersTests
{
    [Test]
    [Description("The members of the organization are listed with their details and role")]
    public async Task Members_AreListedWithTheirRole()
    {
        var response = await Api.Get($"/api/organizations/{SeedDataSet.DevOrg}/users", SeedDataSet.DevUser);

        ApiAssert.IsSuccess(response);
        var members = response.Json.EnumerateArray().ToList();
        foreach (var member in members)
        {
            foreach (var field in new[] { "id", "userId", "firstName", "lastName", "email", "role", "status" })
                Assert.That(member.TryGetProperty(field, out _), Is.True, $"A member has '{field}'");
            Assert.That(member.GetProperty("status").GetString(), Is.EqualTo("Active"), "Only active members are listed");
        }

        // The team list names a member by the id of the user row, which is not the AadId the seed fixes
        string RoleOf(Guid aadId) =>
            members.Single(m => m.GetProperty("userId").GetString() == SeedDataSet.RowId(aadId).ToString()).GetProperty("role").GetString()!;
        Assert.That(RoleOf(SeedDataSet.DevUser), Is.EqualTo("Admin"));
        Assert.That(RoleOf(SeedDataSet.DevViewer), Is.EqualTo("Viewer"));
        Assert.That(members.Any(m => m.GetProperty("userId").GetString() == SeedDataSet.RowId(SeedDataSet.DevMember).ToString()), Is.True, "Dev Member is listed");
    }

    [TestCase("Editor")]
    [TestCase("Viewer")]
    [Description("A member who is not an Admin cannot list the members")]
    public async Task Members_AreRefusedToAMemberWhoIsNotAnAdmin(string role)
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var member = await TestWorld.AddMember(organizationId, admin, role);

        var response = await Api.Get($"/api/organizations/{organizationId}/users", member.Id);

        ApiAssert.IsProblem(response, 403);
    }

    [Test]
    [Description("A member who is not in the organization cannot list its members")]
    public async Task Members_AreRefusedToANonMember()
    {
        var response = await Api.Get($"/api/organizations/{SeedDataSet.DevOrg}/users", SeedDataSet.EmptyAdmin);

        ApiAssert.IsProblem(response, 403);
    }

    [Test]
    [Description("An Admin sees the pending invitations; a Viewer is refused")]
    public async Task PendingInvitations_AreForAdminsOnly()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var viewer = await TestWorld.AddMember(organizationId, admin, "Viewer");
        var email = $"pending-{Guid.NewGuid():N}@myapp.test";
        await Api.Post($"/api/organizations/{organizationId}/collaborators/invite", new { fullName = "Pending Person", email, role = "Editor" }, admin.Id);

        var asAdmin = await Api.Get($"/api/organizations/{organizationId}/invitations/pending", admin.Id);
        var asViewer = await Api.Get($"/api/organizations/{organizationId}/invitations/pending", viewer.Id);

        ApiAssert.IsSuccess(asAdmin);
        var pending = asAdmin.Json.EnumerateArray().Single();
        Assert.That(pending.GetProperty("email").GetString(), Is.EqualTo(email));
        Assert.That(pending.GetProperty("fullName").GetString(), Is.EqualTo("Pending Person"));
        Assert.That(pending.GetProperty("role").GetString(), Is.EqualTo("Editor"));
        Assert.That(pending.TryGetProperty("id", out _), Is.True);
        Assert.That(pending.TryGetProperty("createdAt", out _), Is.True);
        ApiAssert.IsProblem(asViewer, 403);
    }
}
