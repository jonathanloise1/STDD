using Microsoft.Playwright;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Authentication;

/// <summary>
/// The "Page not found" screen, shown for an address that matches no page.
/// </summary>
public sealed class NotFoundPage : BasePage
{
    protected override string? PagePath => "/does-not-exist";

    public NotFoundPage(IPage page) : base(page) { }

    // ----- Actions -----

    /// <summary>"Click the "Homepage" link." Returns when the next screen has settled.</summary>
    public async Task ClickHomepage()
    {
        await HomepageLink.ClickAsync();
        await WaitForPageLoadAsync();
    }

    // ----- State -----

    /// <summary>"Page not found".</summary>
    public ILocator Title => HeadingOf("Page not found");

    /// <summary>The "Homepage" link.</summary>
    public ILocator HomepageLink => Page.GetByRole(AriaRole.Link, new() { Name = "Homepage" });
}
