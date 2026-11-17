using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-TEAM-002: The search ignores upper and lower case
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-TEAM-002")]
[Property("UserStory", "US-ORG-04")]
public class OrgTeam002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Typing a name in lower case finds the member whose name is written with capitals.")]
    public async Task TheSearchIgnoresUpperAndLowerCase()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Type "dev viewer" in the "Search team members..." field.
        await team.TypeSearch("dev viewer");

        // The "Members" table shows the row "Dev Viewer".
        await Expect(team.RowFor("Dev Viewer")).ToBeVisibleAsync();
        // The "Members" table does not show the rows "Dev User" and "Dev Member".
        await Expect(team.RowFor("Dev User")).ToHaveCountAsync(0);
        await Expect(team.RowFor("Dev Member")).ToHaveCountAsync(0);
    }
}
