using MyApp.IntegrationTests.Infrastructure;
using NUnit.Framework;

namespace MyApp.IntegrationTests.Authentication;

/// <summary>
/// An invited person who signs in joins the organization with the invited role. Moved from the old
/// AuthInvite001Tests, which used the seed user "Test UserC"; the test now invites a new user into a new
/// organization so that the seed invitation stays pending for the E2E run.
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("UserStory", "US-AUTH-05")]
public class InvitationAcceptanceTests
{
    [Test]
    [Description("Signing in activates the pending invitation and returns the organization with the invited role")]
    public async Task Sync_AcceptsThePendingInvitation()
    {
        var (organizationId, admin) = await TestWorld.CreateOrganization();
        var invitee = await TestWorld.CreateUser("Invitee");
        var invite = await Api.Post(
            $"/api/organizations/{organizationId}/collaborators/invite",
            new { fullName = invitee.FullName, email = invitee.Email, role = "Editor" },
            admin.Id);
        ApiAssert.IsSuccess(invite, 204);

        var sync = await Api.Post("/api/auth/sync", null, invitee.Id);

        ApiAssert.IsSuccess(sync);
        var joined = sync.Json.GetProperty("organizations").EnumerateArray()
            .SingleOrDefault(o => o.GetProperty("organizationId").GetString() == organizationId.ToString());
        Assert.That(joined.ValueKind, Is.Not.EqualTo(System.Text.Json.JsonValueKind.Undefined), "The organization is listed after the sign-in");
        Assert.That(joined.GetProperty("role").GetString(), Is.EqualTo("Editor"), "The invited role is kept");

        var team = await Api.Get($"/api/organizations/{organizationId}/users", admin.Id);
        var member = team.Json.EnumerateArray().Single(u => u.GetProperty("email").GetString() == invitee.Email);
        Assert.That(member.GetProperty("status").GetString(), Is.EqualTo("Active"));
    }

    [Test]
    [Description("A person who was not invited joins nothing")]
    public async Task Sync_WithoutInvitation_JoinsNothing()
    {
        var stranger = await TestWorld.CreateUser("Stranger");

        var sync = await Api.Post("/api/auth/sync", null, stranger.Id);

        ApiAssert.IsSuccess(sync);
        Assert.That(sync.Json.GetProperty("organizations").GetArrayLength(), Is.EqualTo(0));
    }
}
