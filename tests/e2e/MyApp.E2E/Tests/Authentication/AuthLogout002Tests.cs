using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-LOGOUT-002: After signing out, a page asks to sign in again
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-LOGOUT-002")]
[Property("UserStory", "US-AUTH-03")]
public class AuthLogout002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Once signed out, the app cannot be used: opening a page asks the user to sign in again.")]
    public async Task AfterSigningOutAPageAsksToSignInAgain()
    {
        var address = new AddressPage(Page);
        var menu = new SidebarPage(Page);
        var header = new HeaderPage(Page);
        var menus = new HeaderMenusPage(Page);
        var visitor = new VisitorPage(Page);
        var signInPage = new SignInPage(Page);
        var tasks = new TasksPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // 2. Click the "Dev User" button.
        await menus.ClickAvatar("Dev User");
        // The avatar menu shows "Settings" and "Logout".
        await Expect(menus.SettingsInMenu).ToBeVisibleAsync();
        await Expect(menus.LogoutInMenu).ToBeVisibleAsync();
        // 3. Click the "Logout" button.
        await menus.ClickLogout();
        // 4. Open the Tasks page.
        await visitor.OpenTasksPage();
        // The sign-in page opens (the browser leaves the application; the page itself is the identity
        // provider's and is not covered).
        await signInPage.ExpectOpen();
        // The Tasks page is not shown.
        await Expect(tasks.Table).ToHaveCountAsync(0);
    }
}
