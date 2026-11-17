using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Onboarding;

namespace MyApp.E2E.Tests.Onboarding;

/// <summary>
/// ONBOARD-START-003: A visitor cannot open the setup page
/// </summary>
[TestFixture]
[Category("onboarding")]
[Property("TestCase", "ONBOARD-START-003")]
[Property("UserStory", "US-ONBOARD-01")]
public class OnboardStart003Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Someone who is not signed in is asked to sign in instead of seeing the setup page.")]
    public async Task AVisitorCannotOpenTheSetupPage()
    {
        var visitor = new VisitorPage(Page);
        var signInPage = new SignInPage(Page);
        var setup = new SetupPage(Page);

        // 1. Open the Set Up Your Organization page.
        await visitor.OpenSetupPage();
        // The sign-in page opens (the browser leaves the application; the page itself is the identity
        // provider's and is not covered).
        await signInPage.ExpectOpen();
        // The text "Set Up Your Organization" is not shown.
        await Expect(setup.Title).ToHaveCountAsync(0);
    }
}
