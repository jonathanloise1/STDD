using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-INVITE-003: An invalid email shows a field message
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-INVITE-003")]
[Property("UserStory", "US-ORG-05")]
public class OrgInvite003Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An email that is not a valid address keeps the dialog open and shows a message.")]
    public async Task AnInvalidEmailShowsAFieldMessage()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Invite" button.
        await team.ClickInvite();
        // 3. Type "Jamie Rivera" in the "Full Name" field.
        await team.TypeFullName("Jamie Rivera");
        // 4. Type "jamie.rivera" in the "Email" field.
        await team.TypeInviteEmail("jamie.rivera");
        // 5. Click the "Send Invitation" button.
        await team.ClickSendInvitation();

        // The "Email" field shows "Please enter a valid email address".
        await Expect(team.InviteFieldMessage("Email")).ToHaveTextAsync("Please enter a valid email address");
        // The dialog stays open.
        await Expect(team.InviteDialog).ToBeVisibleAsync();
    }
}
