using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Organizations;

/// <summary>
/// Admins invite people by email; others cannot. Moved from the old OrgInvite001Tests.
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("UserStory", "US-ORG-05")]
public class InviteTeamMemberTests
{
    [Test]
    [Description("An Admin's invitation is accepted and listed as pending with the invited name and role")]
    public async Task Invite_CreatesAPendingInvitation()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var email = $"invite-{Guid.NewGuid():N}@myapp.test";

        var invite = await Api.Post(
            $"/api/organizations/{organizationId}/collaborators/invite",
            new { fullName = "Invited Tester", email, role = "Editor" },
            admin.Id);

        ApiAssert.IsSuccess(invite, 204);
        var pending = await Api.Get($"/api/organizations/{organizationId}/invitations/pending", admin.Id);
        var entry = pending.Json.EnumerateArray().Single(i => string.Equals(i.GetProperty("email").GetString(), email, StringComparison.OrdinalIgnoreCase));
        Assert.That(entry.GetProperty("fullName").GetString(), Is.EqualTo("Invited Tester"));
        Assert.That(entry.GetProperty("role").GetString(), Is.EqualTo("Editor"));
    }

    [TestCase("Editor")]
    [TestCase("Viewer")]
    [Description("A member who is not an Admin cannot invite")]
    public async Task Invite_ByANonAdmin_IsRefused(string role)
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var member = await TestWorld.AddMember(organizationId, admin, role);

        var invite = await Api.Post(
            $"/api/organizations/{organizationId}/collaborators/invite",
            new { fullName = "Should Fail", email = $"fail-{Guid.NewGuid():N}@myapp.test", role = "Viewer" },
            member.Id);

        ApiAssert.IsProblem(invite, 403);
    }
}
