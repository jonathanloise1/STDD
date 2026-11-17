using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Onboarding;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-PROVISION-001: First sign-in goes straight into the app
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-PROVISION-001")]
[Property("UserStory", "US-AUTH-04")]
public class AuthProvision001Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.NewUser;

    [Test]
    [Description("A person who has never signed in can use the app at once, with no registration form, starting from the organization setup.")]
    public async Task FirstSignInGoesStraightIntoTheApp()
    {
        var visitor = new VisitorPage(Page);
        var setup = new SetupPage(Page);

        // 1. Open the home page.
        await visitor.OpenHomePage();
        // 2. Sign in as "New User".
        await visitor.SignInAs(TestUsers.NewUser);
        // No registration form is shown.
        await Expect(visitor.RegistrationForm).ToHaveCountAsync(0);
        // The "Set Up Your Organization" page opens.
        await Expect(setup.Title).ToBeVisibleAsync();
    }
}
