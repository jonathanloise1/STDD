using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Dashboard;

namespace MyApp.E2E.Manual.Authentication;

/// <summary>Screenshots of the menu and the header, as an Admin and as an Editor, for the manual (manual.md 4).</summary>
[TestFixture]
[Category("authentication")]
[Category("manual-capture")]
[Property("UserStory", "US-AUTH-05")]
[Property("UserStory", "US-AUTH-06")]
[Property("UserStory", "US-AUTH-07")]
public class SidebarCapture : ManualCaptureBase
{
    protected override TestUser GetTestUser(string state) =>
        state == nameof(MenuAsEditor) ? TestUsers.MemberUser : TestUsers.AdminUser;

    [Test]
    public async Task MenuAsAdmin()
    {
        var dashboard = new DashboardPage(Page);
        var sidebar = new SidebarPage(Page);
        await dashboard.Open();
        await Expect(dashboard.Greeting).ToBeVisibleAsync();
        await ShootAsync(sidebar);
    }

    [Test]
    public async Task MenuAsEditor()
    {
        var dashboard = new DashboardPage(Page);
        var sidebar = new SidebarPage(Page);
        await dashboard.Open();
        await Expect(dashboard.Greeting).ToBeVisibleAsync();
        await ShootAsync(sidebar);
    }
}
