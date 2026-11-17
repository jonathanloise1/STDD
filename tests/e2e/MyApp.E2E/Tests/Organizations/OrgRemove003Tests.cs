using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-REMOVE-003: Cancelling the removal keeps the member
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-REMOVE-003")]
[Property("UserStory", "US-ORG-07")]
public class OrgRemove003Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Clicking Cancel in the confirmation dialog closes it and the member stays in the table.")]
    public async Task CancellingTheRemovalKeepsTheMember()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Remove" button on the row of "Dev Viewer".
        await team.ClickRemove("Dev Viewer");
        // 3. Click the "Cancel" button in the dialog.
        await team.ClickCancelInRemoveDialog();

        // The dialog is closed.
        await Expect(team.RemoveDialog).ToBeHiddenAsync();
        // The "Members" table still shows "Dev Viewer".
        await Expect(team.RowFor("Dev Viewer")).ToBeVisibleAsync();
    }
}
