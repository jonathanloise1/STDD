using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-ROLE-003: Lowering your own role asks for confirmation
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-ROLE-003")]
[Property("UserStory", "US-ORG-06")]
public class OrgRole003Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An admin who lowers their own role sees a warning and must confirm; going back leaves the role unchanged.")]
    public async Task LoweringYourOwnRoleAsksForConfirmation()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Edit" button on the row of "Dev User".
        await team.ClickEdit("Dev User");
        // 3. Check the "Editor" field.
        await team.CheckRole("Editor");
        // After step 3 the dialog shows "You are removing your own Admin privileges. This action cannot be undone by yourself."
        await Expect(team.DemotionWarning).ToBeVisibleAsync();
        // 4. Click the "Save" button.
        await team.ClickSaveKeepingTheDialogOpen();
        // After step 4 the dialog shows the buttons "Go Back" and "Confirm Demotion".
        await Expect(team.RoleDialogButton("Go Back")).ToBeVisibleAsync();
        await Expect(team.RoleDialogButton("Confirm Demotion")).ToBeVisibleAsync();
        // 5. Click the "Go Back" button.
        await team.ClickGoBack();
        // 6. Click the "Cancel" button.
        await team.ClickCancelInRoleDialog();

        // After step 6 the row of "Dev User" still shows the role "Admin".
        await Expect(team.RoleOf("Dev User")).ToHaveTextAsync("Admin");
    }
}
