using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Manual.Organizations;

/// <summary>Screenshots of the Team page for the manual (manual.md 4).</summary>
[TestFixture]
[Category("organizations")]
[Category("manual-capture")]
[Property("UserStory", "US-ORG-04")]
[Property("UserStory", "US-ORG-05")]
[Property("UserStory", "US-ORG-06")]
[Property("UserStory", "US-ORG-07")]
public class TeamCapture : ManualCaptureBase
{
    protected override TestUser GetTestUser(string state) => TestUsers.AdminUser;

    [Test]
    public async Task Default()
    {
        var team = new TeamPage(Page);
        await team.Open();
        await ShootAsync(team);
    }

    [Test]
    public async Task InviteDialogOpen()
    {
        var team = new TeamPage(Page);
        await team.Open();
        await team.ClickInvite();
        await ShootAsync(team);
    }

    [Test]
    public async Task EditMemberDialogOpen()
    {
        var team = new TeamPage(Page);
        await team.Open();
        await team.ClickEdit(TestUsers.MemberUser.Name);
        await ShootAsync(team);
    }

    [Test]
    public async Task RemoveMemberDialogOpen()
    {
        var team = new TeamPage(Page);
        await team.Open();
        await team.ClickRemove(TestUsers.SpareMember.Name);
        await ShootAsync(team);
    }

    [Test]
    public async Task ListUnavailable()
    {
        var team = new TeamPage(Page);
        ServiceDoesNotAnswer("ORG-TEAM-005");
        await team.Open();
        await WaitForMessageAsync();
        await ShootAsync(team);
    }
}
