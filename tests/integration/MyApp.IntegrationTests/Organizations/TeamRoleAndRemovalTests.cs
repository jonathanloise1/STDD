using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Organizations;

/// <summary>
/// Admins change the role of a member and remove a member. New coverage for the endpoints the E2E tests of
/// the team page reach through the UI; the data is a new organization.
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("UserStory", "US-ORG-06")]
[Property("UserStory", "US-ORG-07")]
public class TeamRoleAndRemovalTests
{
    [Test]
    [Description("An Admin changes the role of a member and the team list shows the new role")]
    public async Task Admin_ChangesTheRole()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var member = await TestWorld.AddMember(organizationId, admin, "Editor");

        var change = await Api.Patch($"/api/organizations/{organizationId}/collaborators/{member.RowId}", new { role = "Viewer" }, admin.Id);

        ApiAssert.IsSuccess(change, 204);
        var team = await Api.Get($"/api/organizations/{organizationId}/users", admin.Id);
        var entry = team.Json.EnumerateArray().Single(u => u.GetProperty("userId").GetString() == member.RowId.ToString());
        Assert.That(entry.GetProperty("role").GetString(), Is.EqualTo("Viewer"));
    }

    [Test]
    [Description("A member who is not an Admin cannot change a role")]
    public async Task NonAdmin_CannotChangeARole()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var editor = await TestWorld.AddMember(organizationId, admin, "Editor");

        var change = await Api.Patch($"/api/organizations/{organizationId}/collaborators/{admin.RowId}", new { role = "Viewer" }, editor.Id);

        ApiAssert.IsProblem(change, 403);
    }

    [Test]
    [Description("An Admin removes a member, who then disappears from the team list")]
    public async Task Admin_RemovesAMember()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var member = await TestWorld.AddMember(organizationId, admin, "Viewer");

        var remove = await Api.Delete($"/api/organizations/{organizationId}/collaborators/{member.RowId}", admin.Id);

        ApiAssert.IsSuccess(remove, 204);
        var team = await Api.Get($"/api/organizations/{organizationId}/users", admin.Id);
        Assert.That(team.Json.EnumerateArray().Any(u => u.GetProperty("userId").GetString() == member.RowId.ToString()), Is.False);
    }

    [Test]
    [Description("An Admin cannot remove themselves")]
    public async Task Admin_CannotRemoveThemselves()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();

        var remove = await Api.Delete($"/api/organizations/{organizationId}/collaborators/{admin.RowId}", admin.Id);

        ApiAssert.IsProblem(remove, 400);
    }
}
