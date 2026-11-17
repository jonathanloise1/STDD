using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-LOGIN-005: A visitor sees no part of the app before signing in
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-LOGIN-005")]
[Property("UserStory", "US-AUTH-02")]
public class AuthLogin005Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Before signing in, a visitor sees only the sign-in page: no menu, no organization name and no data.")]
    public async Task AVisitorSeesNoPartOfTheAppBeforeSigningIn()
    {
        var visitor = new VisitorPage(Page);
        var signInPage = new SignInPage(Page);
        var menu = new SidebarPage(Page);
        var header = new HeaderPage(Page);

        // 1. Open the home page.
        await visitor.OpenHomePage();
        // The sign-in page opens.
        await signInPage.ExpectOpen();
        // The menu entries "Dashboard" and "Tasks" are not shown.
        await Expect(menu.Entry("Dashboard")).ToHaveCountAsync(0);
        await Expect(menu.Entry("Tasks")).ToHaveCountAsync(0);
        // No organization name is shown (the application shows no header at all).
        await Expect(header.Bar).ToHaveCountAsync(0);
    }
}
