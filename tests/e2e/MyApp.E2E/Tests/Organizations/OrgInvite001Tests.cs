using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-INVITE-001: Invite a colleague and see the invitation as pending
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-INVITE-001")]
[Property("UserStory", "US-ORG-05")]
public class OrgInvite001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An admin invites a person as Editor and finds the invitation under Pending Invitations.")]
    public async Task InvitingAColleagueListsTheInvitationAsPending()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);
        var fullName = Unique("Jamie Rivera");
        var email = UniqueEmail("jamie.rivera");

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Invite" button.
        await team.ClickInvite();
        // 3. Type "Jamie Rivera" in the "Full Name" field.
        await team.TypeFullName(fullName);
        // 4. Type "jamie.rivera@example.com" in the "Email" field.
        await team.TypeInviteEmail(email);
        // 5. Check the "Editor" field.
        await team.CheckInviteRole("Editor");
        // 6. Click the "Send Invitation" button.
        await team.ClickSendInvitation();

        // The dialog closes and the message "Invitation sent successfully" appears.
        await Expect(team.InviteDialog).ToBeHiddenAsync();
        await Expect(team.Message("Invitation sent successfully")).ToBeVisibleAsync();
        // "Pending Invitations" lists a row "Jamie Rivera", "jamie.rivera@example.com".
        await Expect(team.PendingRowFor(fullName)).ToContainTextAsync(email);
    }

    /// <summary>
    /// An email no other run has used. A pending invitation cannot be cancelled on the Team page, so a
    /// fixed address would collide with the invitation of the previous run.
    /// </summary>
    private string UniqueEmail(string localPart) => $"{localPart}.{TestRunId[^6..]}@example.com";
}
