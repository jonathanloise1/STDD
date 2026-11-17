using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-INVITE-004: An invited person is not listed as a member yet
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-INVITE-004")]
[Property("UserStory", "US-ORG-05")]
public class OrgInvite004Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An invited person appears under Pending Invitations and not in the Members table.")]
    public async Task AnInvitedPersonIsNotListedAsAMemberYet()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);
        var fullName = Unique("Robin Chen");
        var email = UniqueEmail("robin.chen");

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Invite" button.
        await team.ClickInvite();
        // 3. Type "Robin Chen" in the "Full Name" field.
        await team.TypeFullName(fullName);
        // 4. Type "robin.chen@example.com" in the "Email" field.
        await team.TypeInviteEmail(email);
        // 5. Click the "Send Invitation" button.
        await team.ClickSendInvitation();

        // "Pending Invitations" lists a row "Robin Chen".
        await Expect(team.PendingRowFor(fullName)).ToBeVisibleAsync();
        // The "Members" table does not show a row "Robin Chen".
        await Expect(team.RowFor(fullName)).ToHaveCountAsync(0);
    }

    /// <summary>
    /// An email no other run has used. A pending invitation cannot be cancelled on the Team page, so a
    /// fixed address would collide with the invitation of the previous run.
    /// </summary>
    private string UniqueEmail(string localPart) => $"{localPart}.{TestRunId[^6..]}@example.com";
}
