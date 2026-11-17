using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-ROLE-005: The only Admin cannot give up the Admin role
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-ROLE-005")]
[Property("UserStory", "US-ORG-06")]
public class OrgRole005Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("The only Admin of an organization who confirms a lower role for themselves is told that the organization needs an Admin, and keeps the Admin role.")]
    public async Task TheOnlyAdminCannotGiveUpTheAdminRole()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // "Dev User" is the only Admin of "MyApp Dev Org" (seed data).

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Edit" button on the row of "Dev User".
        await team.ClickEdit("Dev User");
        // 3. Check the "Editor" field.
        await team.CheckRole("Editor");
        // 4. Click the "Save" button.
        await team.ClickSaveKeepingTheDialogOpen();
        // 5. Click the "Confirm Demotion" button.
        await team.ClickConfirmDemotion();

        // The message "An organization needs at least one Admin." appears.
        await Expect(team.Message("An organization needs at least one Admin.")).ToBeVisibleAsync();
        // The row of "Dev User" still shows the role "Admin".
        await Expect(team.RoleOf("Dev User")).ToHaveTextAsync("Admin");
        // The menu still shows the "Team" link.
        await Expect(menu.Entry("Team")).ToBeVisibleAsync();
    }
}
