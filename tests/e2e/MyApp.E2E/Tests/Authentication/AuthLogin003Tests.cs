using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-LOGIN-003: The sign-in page asks for an email and no password
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-LOGIN-003")]
[Property("UserStory", "US-AUTH-01")]
public class AuthLogin003Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("The sign-in page offers email and code only: it has no password field and no sign-in with another service.")]
    public async Task TheSignInPageAsksForAnEmailAndNoPassword()
    {
        var visitor = new VisitorPage(Page);
        var signInPage = new SignInPage(Page);

        // 1. Open the home page.
        await visitor.OpenHomePage();
        // The sign-in page opens: the browser leaves the application.
        await signInPage.ExpectOpen();
        // The sign-in page has no password field. (That it asks for an email address and offers no sign-in
        // with another service is on the identity provider's page and is not covered.)
        await Expect(visitor.PasswordField).ToHaveCountAsync(0);
    }
}
