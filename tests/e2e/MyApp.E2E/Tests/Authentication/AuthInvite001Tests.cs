using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Onboarding;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-INVITE-001: Signing in joins the organization one was invited to
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-INVITE-001")]
[Property("UserStory", "US-AUTH-05")]
public class AuthInvite001Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.UserC;

    [Test]
    [Description("A person with a pending invitation signs in and is a member of the organization without accepting anything and without the setup page.")]
    public async Task SigningInJoinsTheOrganizationOneWasInvitedTo()
    {
        var visitor = new VisitorPage(Page);
        var menu = new SidebarPage(Page);
        var header = new HeaderPage(Page);
        var setup = new SetupPage(Page);

        // 1. Open the home page.
        await visitor.OpenHomePage();
        // 2. Sign in as "Test UserC".
        await visitor.SignInAs(TestUsers.UserC);
        // The Dashboard page opens.
        await Expect(menu.Entry("Dashboard")).ToHaveAttributeAsync("aria-current", "page");
        // The header shows "E2E Invited Org".
        await Expect(header.OrganizationName(TestUsers.OrgInvitedName)).ToBeVisibleAsync();
        // The "Set Up Your Organization" page is not shown.
        await Expect(setup.Title).ToHaveCountAsync(0);
    }
}
