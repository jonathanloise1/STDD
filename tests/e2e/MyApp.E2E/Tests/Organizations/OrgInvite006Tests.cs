using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-INVITE-006: Inviting an email twice shows a message
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-INVITE-006")]
[Property("UserStory", "US-ORG-05")]
public class OrgInvite006Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An admin who invites the same email a second time is told that an invitation is already pending.")]
    public async Task InvitingAnEmailTwiceShowsAMessage()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);
        var fullName = Unique("Sam Okafor");
        var email = UniqueEmail("sam.okafor");

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Invite" button.
        await team.ClickInvite();
        // 3. Type "Sam Okafor" in the "Full Name" field.
        await team.TypeFullName(fullName);
        // 4. Type "sam.okafor@example.com" in the "Email" field.
        await team.TypeInviteEmail(email);
        // 5. Click the "Send Invitation" button.
        await team.ClickSendInvitation();
        // 6. Click the "Invite" button.
        await team.ClickInvite();
        // 7. Type "Sam Okafor" in the "Full Name" field.
        await team.TypeFullName(fullName);
        // 8. Type "sam.okafor@example.com" in the "Email" field.
        await team.TypeInviteEmail(email);
        // 9. Click the "Send Invitation" button.
        await team.ClickSendInvitation();

        // The message "A pending invitation for this email already exists." appears.
        await Expect(team.Message("A pending invitation for this email already exists.")).ToBeVisibleAsync();
        // "Pending Invitations" lists "Sam Okafor" once.
        await Expect(team.PendingRowFor(fullName)).ToHaveCountAsync(1);
    }

    /// <summary>
    /// An email no other run has used. A pending invitation cannot be cancelled on the Team page, so a
    /// fixed address would collide with the invitation of the previous run.
    /// </summary>
    private string UniqueEmail(string localPart) => $"{localPart}.{TestRunId[^6..]}@example.com";
}
