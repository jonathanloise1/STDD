using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-LOGIN-004: Reloading the page keeps the user signed in
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-LOGIN-004")]
[Property("UserStory", "US-AUTH-01")]
public class AuthLogin004Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("A signed-in user who reloads the page stays in the app and is not asked to sign in again.")]
    public async Task ReloadingThePageKeepsTheUserSignedIn()
    {
        var tasks = new TasksPage(Page);
        var menu = new SidebarPage(Page);
        var header = new HeaderPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Reload the page.
        await tasks.Reload();
        // The Tasks page is still shown after the reload.
        await tasks.WaitForList();
        await Expect(menu.Entry("Tasks")).ToHaveAttributeAsync("aria-current", "page");
        // The sign-in page does not open: the application is still shown.
        await Expect(header.Bar).ToBeVisibleAsync();
    }
}
