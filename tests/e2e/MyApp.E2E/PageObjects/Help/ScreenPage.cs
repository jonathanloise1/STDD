using Microsoft.Playwright;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Help;

/// <summary>
/// A screen of the application opened only to use its header, which every screen shares: the Help button is
/// pressed from it. It waits for nothing but the page load, so it also serves a visitor who is sent to the
/// sign-in page. Screens with a page object of their own (Tasks, Dashboard) use that one when they are opened
/// by a signed-in user.
/// </summary>
public sealed class ScreenPage : BasePage
{
    private readonly string _path;

    private ScreenPage(IPage page, string path) : base(page)
    {
        _path = path;
    }

    protected override string? PagePath => _path;

    /// <summary>"Open the Tasks page."</summary>
    public static ScreenPage Tasks(IPage page) => new(page, "/tasks");

    /// <summary>
    /// "Open the Billing page": a screen the application does not have, so it has no manual page and no area
    /// page for its path.
    /// </summary>
    public static ScreenPage Billing(IPage page) => new(page, "/billing");
}
