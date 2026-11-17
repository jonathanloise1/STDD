using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-INVITE-002: Sending an empty invitation shows field messages
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-INVITE-002")]
[Property("UserStory", "US-ORG-05")]
public class OrgInvite002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Leaving Full Name and Email empty keeps the dialog open and shows a message under each field.")]
    public async Task SendingAnEmptyInvitationShowsFieldMessages()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Invite" button.
        await team.ClickInvite();
        // 3. Click the "Send Invitation" button.
        await team.ClickSendInvitation();

        // The "Full Name" field shows "Full name is required".
        await Expect(team.InviteFieldMessage("Full Name")).ToHaveTextAsync("Full name is required");
        // The "Email" field shows "Email address is required".
        await Expect(team.InviteFieldMessage("Email")).ToHaveTextAsync("Email address is required");
        // The dialog stays open.
        await Expect(team.InviteDialog).ToBeVisibleAsync();
    }
}
