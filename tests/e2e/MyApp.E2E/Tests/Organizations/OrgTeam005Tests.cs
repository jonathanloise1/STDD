using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-TEAM-005: Loading the team when the service does not answer shows a message
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-TEAM-005")]
[Property("UserStory", "US-ORG-04")]
public class OrgTeam005Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("When the service does not answer while the members are loaded, the admin is told so instead of seeing an empty team.")]
    public async Task LoadingTheTeamWhenTheServiceDoesNotAnswerShowsAMessage()
    {
        var menu = new SidebarPage(Page);
        var team = new TeamPage(Page);

        // The service does not answer when the team members are loaded.
        ServiceDoesNotAnswer();

        // 1. Click the "Team" link in the menu.
        await menu.ClickTeam();

        // The message "The service did not answer. Try again in a few minutes." appears.
        await Expect(team.Message("The service did not answer. Try again in a few minutes.")).ToBeVisibleAsync();
        // The "Members" table does not show "No team members found".
        await Expect(team.NoMembersMessage).ToHaveCountAsync(0);
    }
}
