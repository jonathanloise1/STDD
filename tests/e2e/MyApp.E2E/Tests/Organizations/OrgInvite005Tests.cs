using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-INVITE-005: Inviting an existing member shows a message
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-INVITE-005")]
[Property("UserStory", "US-ORG-05")]
public class OrgInvite005Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An admin who invites the email of a current member is told that the person is already a member.")]
    public async Task InvitingAnExistingMemberShowsAMessage()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // "member@myapp.local" is the email of the member "Dev Member" (seed data).

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Invite" button.
        await team.ClickInvite();
        // 3. Type "Dev Member" in the "Full Name" field.
        await team.TypeFullName("Dev Member");
        // 4. Type "member@myapp.local" in the "Email" field.
        await team.TypeInviteEmail("member@myapp.local");
        // 5. Click the "Send Invitation" button.
        await team.ClickSendInvitation();

        // The message "User is already a member of this organization." appears.
        await Expect(team.Message("User is already a member of this organization.")).ToBeVisibleAsync();
        // "Pending Invitations" does not list "Dev Member".
        await Expect(team.PendingRowFor("Dev Member")).ToHaveCountAsync(0);
    }
}
