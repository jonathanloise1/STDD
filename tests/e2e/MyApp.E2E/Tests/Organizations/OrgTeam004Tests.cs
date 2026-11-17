using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-TEAM-004: An editor cannot open the Team page
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-TEAM-004")]
[Property("UserStory", "US-ORG-04")]
public class OrgTeam004Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("An editor has no \"Team\" menu item, and opening the Team page by its address leads to the Dashboard with a message.")]
    public async Task OpeningTheTeamPageLeadsToTheDashboard()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);
        var noAccess = new NoAccessPage(Page);

        // 1. Open the Team page.
        await team.Open();

        // The Dashboard page opens with the message "You don't have access to this page."
        await noAccess.ExpectDashboardOpen();
        await Expect(noAccess.NoAccessMessage).ToBeVisibleAsync();
        // The page does not show "Team Management".
        await Expect(team.AnyTitle).ToHaveCountAsync(0);
        // The menu has no "Team" link.
        await Expect(menu.Entry("Team")).ToHaveCountAsync(0);
    }
}
