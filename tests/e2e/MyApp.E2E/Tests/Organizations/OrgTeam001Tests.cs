using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-TEAM-001: The Team page lists the members with their roles
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-TEAM-001")]
[Property("UserStory", "US-ORG-04")]
public class OrgTeam001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An admin opens the Team page from the menu and sees each member with the role.")]
    public async Task TheTeamPageListsTheMembersWithTheirRoles()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();

        // The Team page opens with the title "Team Management - MyApp Dev Org".
        await Expect(team.Title(TestUsers.OrgDevName)).ToBeVisibleAsync();
        // The "Members" table has a row "Dev User", "dev@myapp.local", "Admin".
        await Expect(team.EmailOf("Dev User")).ToHaveTextAsync("dev@myapp.local");
        await Expect(team.RoleOf("Dev User")).ToHaveTextAsync("Admin");
        // The "Members" table has a row "Dev Member", "member@myapp.local", "Editor".
        await Expect(team.EmailOf("Dev Member")).ToHaveTextAsync("member@myapp.local");
        await Expect(team.RoleOf("Dev Member")).ToHaveTextAsync("Editor");
        // The "Members" table has a row "Dev Viewer", "viewer@myapp.local", "Viewer".
        await Expect(team.EmailOf("Dev Viewer")).ToHaveTextAsync("viewer@myapp.local");
        await Expect(team.RoleOf("Dev Viewer")).ToHaveTextAsync("Viewer");
    }
}
