using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-LOGIN-001: Signing in opens the Dashboard page
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-LOGIN-001")]
[Property("UserStory", "US-AUTH-01")]
public class AuthLogin001Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("A member who signs in with their email address and code reaches the Dashboard page and is shown as signed in.")]
    public async Task SigningInOpensTheDashboardPage()
    {
        var visitor = new VisitorPage(Page);
        var menu = new SidebarPage(Page);
        var header = new HeaderPage(Page);
        var menus = new HeaderMenusPage(Page);

        // 1. Open the home page.
        await visitor.OpenHomePage();
        // 2. Sign in as Admin.
        await visitor.SignInAs(TestUsers.AdminUser);
        // The Dashboard page opens.
        await Expect(menu.Entry("Dashboard")).ToHaveAttributeAsync("aria-current", "page");
        // The "Dev User" button at the top right shows "DU".
        await Expect(menus.AvatarButton("Dev User")).ToHaveTextAsync("DU");
        // The header shows "MyApp Dev Org".
        await Expect(header.OrganizationName(TestUsers.OrgDevName)).ToBeVisibleAsync();
    }
}
