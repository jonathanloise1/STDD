using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-REMOVE-004: Removing a member already removed shows a message
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-REMOVE-004")]
[Property("UserStory", "US-ORG-07")]
public class OrgRemove004Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An admin who confirms the removal of a member that another admin has just removed is told that the member is no longer in the team.")]
    public async Task RemovingAMemberAlreadyRemovedShowsAMessage()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);
        // A second session is signed in as Admin of "MyApp Dev Org".
        var admin = await StartSecondSession(TestUsers.AdminUser);
        var adminMenu = new SidebarPage(admin);
        var adminTeam = new TeamPage(admin);

        // "MyApp Dev Org" has a Viewer named "Former Member" (seed data).
        // This test removes "Former Member" for good: the member is back only when the seed runs again.
        // A repeat run against the same database fails at step 2 (no row "Former Member") until then.

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Remove" button on the row of "Former Member".
        await team.ClickRemove("Former Member");
        // 3. As Admin: Click the "Team" link in the menu.
        await adminMenu.ClickTeam();
        // 4. As Admin: Click the "Remove" button on the row of "Former Member".
        await adminTeam.ClickRemove("Former Member");
        // 5. As Admin: Click the "Remove" button in the dialog.
        await adminTeam.ClickRemoveInDialog();
        // 6. Click the "Remove" button in the dialog.
        await team.ClickRemoveInDialog();

        // The message "This member is no longer in the team." appears.
        await Expect(team.Message("This member is no longer in the team.")).ToBeVisibleAsync();
        // The "Members" table does not show "Former Member".
        await team.ExpectNotListed("Former Member");
    }
}
