using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-LOGIN-006: Signing in when the service does not answer shows a message
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-LOGIN-006")]
[Property("UserStory", "US-AUTH-01")]
public class AuthLogin006Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("When the service does not answer while the account is loaded after signing in, the user is told so instead of seeing an empty app.")]
    public async Task SigningInWhenTheServiceDoesNotAnswerShowsAMessage()
    {
        var visitor = new VisitorPage(Page);
        var address = new AddressPage(Page);
        var header = new HeaderPage(Page);

        // The service does not answer when the account is loaded after signing in.
        ServiceDoesNotAnswer();
        // 1. Open the home page.
        await visitor.OpenHomePage();
        // 2. Sign in as Admin.
        await visitor.SignInAs(TestUsers.AdminUser);
        // The message "The service did not answer. Try again in a few minutes." appears.
        await Expect(address.Message("The service did not answer. Try again in a few minutes.")).ToBeVisibleAsync();
        // The header does not show "MyApp Dev Org".
        await Expect(header.OrganizationName(TestUsers.OrgDevName)).ToHaveCountAsync(0);
    }
}
