using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-INVITE-007: The invitation dialog starts with the Viewer role
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-INVITE-007")]
[Property("UserStory", "US-ORG-05")]
public class OrgInvite007Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("The role Viewer is selected when the dialog opens.")]
    public async Task TheInvitationDialogStartsWithTheViewerRole()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Invite" button.
        await team.ClickInvite();

        // The "Invite Team Member" dialog shows "Admin", "Editor" and "Viewer", each with a description.
        await Expect(team.InviteDialog).ToBeVisibleAsync();
        foreach (var role in new[] { "Admin", "Editor", "Viewer" })
        {
            await Expect(team.InviteRoleRadio(role)).ToBeVisibleAsync();
            await Expect(team.InviteRoleDescription(role)).ToBeVisibleAsync();
        }
        // "Viewer" is selected.
        await Expect(team.InviteRoleRadio("Viewer")).ToBeCheckedAsync();
    }
}
