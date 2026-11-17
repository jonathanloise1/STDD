using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-LOGOUT-001: Signing out opens the sign-in page
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-LOGOUT-001")]
[Property("UserStory", "US-AUTH-03")]
public class AuthLogout001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("A signed-in user who clicks \"Logout\" in the avatar menu is signed out and sees the sign-in page.")]
    public async Task SigningOutOpensTheSignInPage()
    {
        var address = new AddressPage(Page);
        var menu = new SidebarPage(Page);
        var header = new HeaderPage(Page);
        var menus = new HeaderMenusPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // 2. Click the "Dev User" button.
        await menus.ClickAvatar("Dev User");
        // The avatar menu shows "Settings" and "Logout".
        await Expect(menus.SettingsInMenu).ToBeVisibleAsync();
        await Expect(menus.LogoutInMenu).ToBeVisibleAsync();
        // 3. Click the "Logout" button.
        await menus.ClickLogout();
        // The sign-in page opens: the application shows nothing of its own any more. (The sign-in page is
        // the identity provider's and is not covered.)
        await Expect(header.Bar).ToHaveCountAsync(0);
        await Expect(menu.Menu).ToHaveCountAsync(0);
    }
}
