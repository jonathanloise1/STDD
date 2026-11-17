using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-ROLE-006: Saving the role of a removed member shows a message
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-ROLE-006")]
[Property("UserStory", "US-ORG-06")]
public class OrgRole006Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An admin who saves a new role for a member that another admin has just removed is told that the member is no longer in the team.")]
    public async Task SavingTheRoleOfARemovedMemberShowsAMessage()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);
        // A second session is signed in as Admin of "MyApp Dev Org".
        var admin = await StartSecondSession(TestUsers.AdminUser);
        var adminMenu = new SidebarPage(admin);
        var adminTeam = new TeamPage(admin);

        // "MyApp Dev Org" has a Viewer named "Leaving Member" (seed data).
        // This test removes "Leaving Member" for good: the member is back only when the seed runs again.
        // A repeat run against the same database fails at step 2 (no row "Leaving Member") until then.

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Edit" button on the row of "Leaving Member".
        await team.ClickEdit("Leaving Member");
        // 3. Check the "Editor" field.
        await team.CheckRole("Editor");
        // 4. As Admin: Click the "Team" link in the menu.
        await adminMenu.ClickTeam();
        // 5. As Admin: Click the "Remove" button on the row of "Leaving Member".
        await adminTeam.ClickRemove("Leaving Member");
        // 6. As Admin: Click the "Remove" button in the dialog.
        await adminTeam.ClickRemoveInDialog();
        // 7. Click the "Save" button.
        await team.ClickSave();

        // The message "This member is no longer in the team." appears.
        await Expect(team.Message("This member is no longer in the team.")).ToBeVisibleAsync();
        // The "Members" table does not show "Leaving Member".
        await team.ExpectNotListed("Leaving Member");
    }
}
