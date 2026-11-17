using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Onboarding;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-PROVISION-002: Signing in again does not create a second account
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-PROVISION-002")]
[Property("UserStory", "US-AUTH-04")]
public class AuthProvision002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("A returning user who signs in a second time finds the same account and the same single organization.")]
    public async Task SigningInAgainDoesNotCreateASecondAccount()
    {
        var address = new AddressPage(Page);
        var menu = new SidebarPage(Page);
        var header = new HeaderPage(Page);
        var menus = new HeaderMenusPage(Page);
        var visitor = new VisitorPage(Page);
        var setup = new SetupPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // 2. Click the "Dev User" button.
        await menus.ClickAvatar("Dev User");
        // 3. Click the "Logout" button.
        await menus.ClickLogout();
        // 4. Sign in as Admin.
        await visitor.SignInAs(TestUsers.AdminUser);
        // The Dashboard page opens.
        await Expect(menu.Entry("Dashboard")).ToHaveAttributeAsync("aria-current", "page");
        // The header shows "MyApp Dev Org" once, without a list of other organizations.
        await Expect(header.OrganizationName(TestUsers.OrgDevName)).ToHaveCountAsync(1);
        await Expect(menus.OrganizationSwitcher(TestUsers.OrgDevName)).ToHaveCountAsync(0);
        // The "Set Up Your Organization" page is not shown.
        await Expect(setup.Title).ToHaveCountAsync(0);
    }
}
