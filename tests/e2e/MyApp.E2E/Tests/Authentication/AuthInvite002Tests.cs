using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-INVITE-002: An invited member has the invited role
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-INVITE-002")]
[Property("UserStory", "US-AUTH-05")]
public class AuthInvite002Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.UserC;

    [Test]
    [Description("A person invited as Editor can create tasks after joining but sees no management pages.")]
    public async Task AnInvitedMemberHasTheInvitedRole()
    {
        var visitor = new VisitorPage(Page);
        var menu = new SidebarPage(Page);
        var tasks = new TasksPage(Page);

        // 1. Open the home page.
        await visitor.OpenHomePage();
        // 2. Sign in as "Test UserC".
        await visitor.SignInAs(TestUsers.UserC);
        // 3. Click the "Tasks" link in the menu.
        await menu.ClickTasks();
        // The menu shows "Dashboard" and "Tasks" and no "Settings".
        await Expect(menu.Entry("Dashboard")).ToBeVisibleAsync();
        await Expect(menu.Entry("Tasks")).ToBeVisibleAsync();
        await Expect(menu.Entry("Settings")).ToHaveCountAsync(0);
        // The Tasks page shows the "New Task" button.
        await Expect(tasks.NewTaskButton).ToBeVisibleAsync();
    }
}
