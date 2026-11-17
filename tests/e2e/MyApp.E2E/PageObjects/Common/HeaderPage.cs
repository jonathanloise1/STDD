using Microsoft.Playwright;

namespace MyApp.E2E.PageObjects.Common;

/// <summary>
/// The bar at the top of every screen: the organization name (or switcher), the language button and the
/// avatar menu of the signed-in user ("Settings", "Logout").
/// </summary>
public sealed class HeaderPage : BasePage
{
    public HeaderPage(IPage page) : base(page) { }

    // ----- Actions -----

    /// <summary>"Click the "Change language" button."</summary>
    public Task ClickChangeLanguage() => ClickAsync(Button("Change language"));

    /// <summary>"Click the "Italiano" button." Names are the languages' own: "English", "Italiano".</summary>
    public Task ClickLanguage(string language) => ClickAsync(Button(language));

    /// <summary>"Click the avatar button": the button named after the signed-in user, for example "Dev User".</summary>
    public Task ClickAvatar(string userName) => ClickAsync(Button(userName));

    /// <summary>"Click the "Logout" button" in the avatar menu.</summary>
    public Task ClickLogout() => ClickAsync(Button("Logout"));

    /// <summary>"Click the "Settings" button" in the avatar menu.</summary>
    public Task ClickSettingsInMenu() => ClickAsync(Button("Settings"));

    /// <summary>
    /// "Click the language button" in whatever language the application shows: its accessible name follows the
    /// language ("Change language", "Cambia lingua"), so it is found by its test id.
    /// </summary>
    public Task ClickLanguageSelector() => ClickAsync(Page.GetByTestId("btn-language-selector"));

    // ----- State -----

    /// <summary>The bar itself. It has no accessible name; the page object is its only reader.</summary>
    public ILocator Bar => Page.Locator("header.app-header");

    /// <summary>The organization name shown on the left of the bar.</summary>
    public ILocator OrganizationName(string name) => Bar.GetByText(name, new() { Exact = true });

    /// <summary>The language button; its text is the language code, for example "EN".</summary>
    public ILocator ChangeLanguageButton => Button("Change language");

    private async Task ClickAsync(ILocator target)
    {
        await target.ClickAsync();
        await WaitForPageLoadAsync();
    }
}
