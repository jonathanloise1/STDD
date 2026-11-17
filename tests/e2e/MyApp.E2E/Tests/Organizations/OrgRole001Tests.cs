using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-ROLE-001: Give a viewer the Editor role
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-ROLE-001")]
[Property("UserStory", "US-ORG-06")]
public class OrgRole001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An admin changes a member from Viewer to Editor and sees the new role in the table.")]
    public async Task GivingAViewerTheEditorRoleShowsTheNewRole()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Edit" button on the row of "Dev Viewer".
        await team.ClickEdit("Dev Viewer");
        // 3. Check the "Editor" field.
        await team.CheckRole("Editor");
        // 4. Click the "Save" button.
        await team.ClickSave();

        // After the first save, the dialog closes and the message "Member updated successfully" appears.
        await Expect(team.EditDialog).ToBeHiddenAsync();
        await Expect(team.Message("Member updated successfully")).ToBeVisibleAsync();
        // After the first save, the row of "Dev Viewer" shows the role "Editor".
        await Expect(team.RoleOf("Dev Viewer")).ToHaveTextAsync("Editor");

        // 5. Click the "Edit" button on the row of "Dev Viewer".
        await team.ClickEdit("Dev Viewer");
        // 6. Check the "Viewer" field.
        await team.CheckRole("Viewer");
        // 7. Click the "Save" button.
        await team.ClickSave();

        // After the second save, the row of "Dev Viewer" shows the role "Viewer" again.
        await Expect(team.RoleOf("Dev Viewer")).ToHaveTextAsync("Viewer");
    }

    /// <summary>
    /// The test gives the role back in its own steps; this does the same when a failure stopped it half way,
    /// so the next test still finds "Dev Viewer" as a Viewer (development.md 10.3). Best effort.
    /// </summary>
    protected override async Task CleanUpAsync()
    {
        try
        {
            var team = new TeamPage(Page);
            await team.Open();
            await team.EnsureRole("Dev Viewer", "Viewer");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[E2E] Clean-up of the role failed: {ex.Message}");
        }
    }
}
