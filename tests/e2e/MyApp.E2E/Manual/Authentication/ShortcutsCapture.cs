using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Dashboard;

namespace MyApp.E2E.Manual.Authentication;

/// <summary>Screenshots of the "Keyboard Shortcuts" dialog for the manual (manual.md 4).</summary>
[TestFixture]
[Category("authentication")]
[Category("manual-capture")]
[Property("UserStory", "US-AUTH-09")]
public class ShortcutsCapture : ManualCaptureBase
{
    protected override TestUser GetTestUser(string state) =>
        state == nameof(DialogOpenAsViewer) ? TestUsers.ViewerUser : TestUsers.AdminUser;

    [Test]
    public async Task DialogOpen()
    {
        var dashboard = new DashboardPage(Page);
        var shortcuts = new ShortcutsPage(Page);
        await dashboard.Open();
        await shortcuts.PressQuestionMark();
        await ShootAsync(shortcuts);
    }

    [Test]
    public async Task DialogOpenAsViewer()
    {
        var dashboard = new DashboardPage(Page);
        var shortcuts = new ShortcutsPage(Page);
        await dashboard.Open();
        await shortcuts.PressQuestionMark();
        await ShootAsync(shortcuts);
    }
}
