using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-LOGIN-002: Opening the Tasks page when not signed in asks to sign in
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-LOGIN-002")]
[Property("UserStory", "US-AUTH-02")]
public class AuthLogin002Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A visitor who opens the Tasks page is taken to the sign-in page, and after signing in the Tasks page opens.")]
    public async Task OpeningTheTasksPageWhenNotSignedInAsksToSignIn()
    {
        var visitor = new VisitorPage(Page);
        var signInPage = new SignInPage(Page);
        var menu = new SidebarPage(Page);
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await visitor.OpenTasksPage();
        // After step 1, the sign-in page opens (the browser leaves the application; that it asks for an
        // email address is on the identity provider's page and is not covered); the Tasks page is not shown.
        await signInPage.ExpectOpen();
        await Expect(tasks.Table).ToHaveCountAsync(0);
        // 2. Sign in as Editor.
        await visitor.SignInAs(TestUsers.MemberUser);
        // After step 2, the Tasks page opens.
        await Expect(menu.Entry("Tasks")).ToHaveAttributeAsync("aria-current", "page");
    }
}
