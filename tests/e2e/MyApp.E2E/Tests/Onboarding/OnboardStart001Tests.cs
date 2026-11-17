using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Onboarding;

namespace MyApp.E2E.Tests.Onboarding;

/// <summary>
/// ONBOARD-START-001: A user without an organization is taken to the setup page
/// </summary>
[TestFixture]
[Category("onboarding")]
[Property("TestCase", "ONBOARD-START-001")]
[Property("UserStory", "US-ONBOARD-01")]
public class OnboardStart001Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.NewUser;

    [Test]
    [Description("A user who belongs to no organization sees the setup page right after signing in, with no way to skip it.")]
    public async Task AUserWithoutAnOrganizationIsTakenToTheSetupPage()
    {
        var visitor = new VisitorPage(Page);
        var setup = new SetupPage(Page);

        // 1. Open the home page.
        await visitor.OpenHomePage();
        // 2. Sign in as "New User".
        await visitor.SignInAs(TestUsers.NewUser);
        // The "Set Up Your Organization" page opens.
        await Expect(setup.Title).ToBeVisibleAsync();
        // The wizard shows the step "Organization Details" and a "Submit" button.
        await Expect(setup.StepTitle).ToBeVisibleAsync();
        await Expect(setup.SubmitButton).ToBeVisibleAsync();
        // No "Skip for now" link is shown.
        await Expect(setup.SkipLink).ToHaveCountAsync(0);
    }
}
