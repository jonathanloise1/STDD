using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Dashboard;

namespace MyApp.E2E.Manual.Authentication;

/// <summary>Screenshots of the avatar menu and the language menu of the header for the manual (manual.md 4).</summary>
[TestFixture]
[Category("authentication")]
[Category("manual-capture")]
[Property("UserStory", "US-AUTH-01")]
[Property("UserStory", "US-AUTH-03")]
[Property("UserStory", "US-AUTH-07")]
[Property("UserStory", "US-AUTH-08")]
public class HeaderMenusCapture : ManualCaptureBase
{
    protected override TestUser GetTestUser(string state) =>
        state == nameof(AvatarMenuOpenAsEditor) ? TestUsers.MemberUser : TestUsers.AdminUser;

    [Test]
    public async Task AvatarMenuOpen()
    {
        var dashboard = new DashboardPage(Page);
        var menus = new HeaderMenusPage(Page);
        await dashboard.Open();
        await menus.ClickAvatar(TestUsers.AdminUser.Name);
        await ShootAsync(menus);
    }

    [Test]
    public async Task AvatarMenuOpenAsEditor()
    {
        var dashboard = new DashboardPage(Page);
        var menus = new HeaderMenusPage(Page);
        await dashboard.Open();
        await menus.ClickAvatar(TestUsers.MemberUser.Name);
        await ShootAsync(menus);
    }

    [Test]
    public async Task LanguageMenuOpen()
    {
        var dashboard = new DashboardPage(Page);
        var menus = new HeaderMenusPage(Page);
        await dashboard.Open();
        await menus.ClickLanguageButton();
        await ShootAsync(menus);
    }
}
