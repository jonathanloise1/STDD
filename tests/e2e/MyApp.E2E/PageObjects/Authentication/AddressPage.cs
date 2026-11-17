using Microsoft.Playwright;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Authentication;

/// <summary>
/// Opening a page of the application by its address, as a signed-in user typing it in the browser
/// ("Open the Team page."), and the state every page shares: the messages of the application and the mark
/// that tells whether the page was reloaded. The menu and the header are the shared parts
/// <see cref="SidebarPage"/> and <see cref="HeaderPage"/>.
/// </summary>
public sealed class AddressPage : BasePage
{
    public AddressPage(IPage page) : base(page) { }

    // ----- Actions: "Open the {name} page." -----

    /// <summary>"Open the Dashboard page."</summary>
    public Task OpenDashboardPage() => OpenAsync("/dashboard");

    /// <summary>"Open the Team page."</summary>
    public Task OpenTeamPage() => OpenAsync("/settings/team");

    /// <summary>"Open the Organization page."</summary>
    public Task OpenOrganizationPage() => OpenAsync("/settings/organization");

    /// <summary>"Open the Audit Log page."</summary>
    public Task OpenAuditLogPage() => OpenAsync("/audit");

    /// <summary>
    /// Goes to the address, waits for the screen to settle and for the application to show whatever it
    /// decided to show: the page, the page it redirected to, or a message.
    /// </summary>
    public async Task OpenAsync(string path)
    {
        LogStep("Open", path);
        await Page.GotoAsync($"{TestConfiguration.BaseUrl}{path}");
        await WaitForPageLoadAsync();
        await WaitUntilAppIsShown();
    }

    /// <summary>
    /// Waits until the application has replaced its loading image with a screen: a page, or the message of
    /// a failed start. It never returns for a visitor, whose application shows nothing.
    /// </summary>
    public Task WaitUntilAppIsShown() =>
        Page.WaitForFunctionAsync(
            "() => !!document.querySelector('.app') && !document.querySelector('img[alt=\"Loading...\"]')");

    /// <summary>
    /// Marks the page that is open, so <see cref="MarkedPage"/> can tell later whether it was reloaded.
    /// It is not a step of a test case.
    /// </summary>
    public Task MarkThePage() => Page.EvaluateAsync("() => document.body.setAttribute('data-e2e-marker', 'loaded')");

    // ----- State -----

    /// <summary>The marked page: it exists as long as the page was not reloaded since <see cref="MarkThePage"/>.</summary>
    public ILocator MarkedPage => Page.Locator("body[data-e2e-marker]");

    /// <summary>Every message the application shows (toasts and inline alerts).</summary>
    public ILocator Alerts => Page.GetByRole(AriaRole.Alert);
}
