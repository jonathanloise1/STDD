using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-ROLE-004: Save stays disabled until something changes
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-ROLE-004")]
[Property("UserStory", "US-ORG-06")]
public class OrgRole004Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("The Save button is disabled when the dialog opens and enabled after a role is chosen.")]
    public async Task SaveStaysDisabledUntilSomethingChanges()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Click the "Edit" button on the row of "Dev Viewer".
        await team.ClickEdit("Dev Viewer");

        // When the dialog opens, "Viewer" is selected and the "Save" button is disabled.
        await Expect(team.RoleRadio("Viewer")).ToBeCheckedAsync();
        await Expect(team.SaveButton).ToBeDisabledAsync();

        // 3. Check the "Editor" field.
        await team.CheckRole("Editor");

        // The test case says "After step 4", but it has three steps: this is the result of step 3.
        // The "Save" button is enabled.
        await Expect(team.SaveButton).ToBeEnabledAsync();
    }
}
