using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Onboarding;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-PROVISION-003: A new account starts in English
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-PROVISION-003")]
[Property("UserStory", "US-AUTH-04")]
public class AuthProvision003Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.NewUser;

    [Test]
    [Description("A new user who has chosen no language sees the app in English.")]
    public async Task ANewAccountStartsInEnglish()
    {
        var visitor = new VisitorPage(Page);
        var setup = new SetupPage(Page);

        // 1. Open the home page.
        await visitor.OpenHomePage();
        // 2. Sign in as "New User".
        await visitor.SignInAs(TestUsers.NewUser);
        // The page title "Set Up Your Organization" is shown in English.
        await Expect(setup.Title).ToBeVisibleAsync();
        // The "Submit" button is shown in English.
        await Expect(setup.SubmitButton).ToBeVisibleAsync();
    }
}
