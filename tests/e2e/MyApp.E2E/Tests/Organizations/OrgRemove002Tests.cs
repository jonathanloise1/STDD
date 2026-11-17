using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-REMOVE-002: Admin rows have no remove button
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-REMOVE-002")]
[Property("UserStory", "US-ORG-07")]
public class OrgRemove002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("The table offers removal for Editors and Viewers but not for Admins.")]
    public async Task AdminRowsHaveNoRemoveButton()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();

        // The row of "Dev Member" (Editor) has a "Remove" button.
        await Expect(team.RemoveButtonOf("Dev Member")).ToBeVisibleAsync();
        // The row of "Dev Viewer" (Viewer) has a "Remove" button.
        await Expect(team.RemoveButtonOf("Dev Viewer")).ToBeVisibleAsync();
        // The row of "Dev User" (Admin) has no "Remove" button.
        await Expect(team.RowFor("Dev User")).ToBeVisibleAsync();
        await Expect(team.RemoveButtonOf("Dev User")).ToHaveCountAsync(0);
    }
}
