using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-TEAM-003: A search without results shows a message
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-TEAM-003")]
[Property("UserStory", "US-ORG-04")]
public class OrgTeam003Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Typing text that matches nobody shows a message in the table.")]
    public async Task ASearchWithoutResultsShowsAMessage()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();
        // 2. Type "zzzz" in the "Search team members..." field.
        await team.TypeSearch("zzzz");

        // The table shows "No team members match your search".
        await Expect(team.NoMatchMessage).ToBeVisibleAsync();
    }
}
