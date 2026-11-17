using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-REMOVE-001: Remove a member from the team
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-REMOVE-001")]
[Property("UserStory", "US-ORG-07")]
public class OrgRemove001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An admin removes a viewer after confirming and the member leaves the table.")]
    public async Task RemovingAMemberTakesThemOutOfTheTable()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // "MyApp Dev Org" has a Viewer named "Spare Member" (seed data).
        // This test removes "Spare Member" for good: the member is back only when the seed runs again.
        // A repeat run against the same database fails at step 2 (no row "Spare Member") until then.

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Remove" button on the row of "Spare Member".
        await team.ClickRemove("Spare Member");

        // The dialog "Confirm Removal" asked "Are you sure you want to remove Spare Member from the team? This action cannot be undone."
        await Expect(team.RemoveDialog).ToBeVisibleAsync();
        await Expect(team.RemoveQuestion("Spare Member")).ToBeVisibleAsync();

        // 3. Click the "Remove" button in the dialog.
        await team.ClickRemoveInDialog();

        // The message "Member removed successfully" appears.
        await Expect(team.Message("Member removed successfully")).ToBeVisibleAsync();
        // The "Members" table does not show "Spare Member".
        await team.ExpectNotListed("Spare Member");
    }
}
