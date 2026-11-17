using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Onboarding;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Onboarding;

/// <summary>
/// ONBOARD-START-004: Any page opened without an organization shows the setup page
/// </summary>
[TestFixture]
[Category("onboarding")]
[Property("TestCase", "ONBOARD-START-004")]
[Property("UserStory", "US-ONBOARD-01")]
public class OnboardStart004Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.NewUser;

    [Test]
    [Description("A user who belongs to no organization and opens another page of the app sees the setup page instead.")]
    public async Task AnyPageOpenedWithoutAnOrganizationShowsTheSetupPage()
    {
        var visitor = new VisitorPage(Page);
        var setup = new SetupPage(Page);
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await visitor.OpenTasksPage();
        // 2. Sign in as "New User".
        await visitor.SignInAs(TestUsers.NewUser);
        // The "Set Up Your Organization" page opens.
        await Expect(setup.Title).ToBeVisibleAsync();
        // The Tasks page is not shown.
        await Expect(tasks.Table).ToHaveCountAsync(0);
        await Expect(tasks.NewTaskButton).ToHaveCountAsync(0);
    }
}
