using Microsoft.Playwright;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Authentication;

/// <summary>
/// The menus of the header: the avatar menu ("Settings", "Logout"), the language menu and the organization
/// name or switcher. The bar itself and the organization name are in <see cref="HeaderPage"/>.
///
/// The language button and "Logout" are found by test id because their names change with the language of
/// the application ("Change language" / "Cambia lingua", "Logout" / "Esci").
/// </summary>
public sealed class HeaderMenusPage : BasePage
{
    public HeaderMenusPage(IPage page) : base(page) { }

    // ----- Actions: the avatar menu -----

    /// <summary>"Click the "Dev User" button": the avatar button, named after the signed-in user.</summary>
    public Task ClickAvatar(string userName) => AvatarButton(userName).ClickAsync();

    /// <summary>
    /// "Click the "Logout" button." The application signs the user out; the part of signing out that lives in
    /// the identity provider is done by the E2E session. Returns when the application has taken its pages away.
    /// </summary>
    public async Task ClickLogout()
    {
        LogStep("Click", "Logout");
        await LogoutInMenu.ClickAsync();
        await E2EImpersonation.EndSessionAsync(Page.Context);
        await Assertions.Expect(Bar).ToHaveCountAsync(0);
    }

    // ----- Actions: the language menu -----

    /// <summary>"Click the "Change language" button." Works in every language of the application.</summary>
    public Task ClickLanguageButton() => LanguageButton.ClickAsync();

    /// <summary>
    /// "Click the "Italiano" button" of the language menu ("English", "Italiano"). Returns when the request
    /// that saves the choice has finished, whatever the outcome.
    /// </summary>
    public async Task ClickLanguage(string language)
    {
        LogStep("Click language", language);
        await LanguageMenu.GetByRole(AriaRole.Button, new() { Name = language, Exact = true }).ClickAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>
    /// Puts the language back to English through the buttons a user clicks; does nothing when it already is
    /// English. For clean-up after a test that changed it.
    /// </summary>
    public async Task SwitchToEnglishIfNeeded()
    {
        var text = await LanguageButton.InnerTextAsync();
        if (text.Contains("EN", StringComparison.OrdinalIgnoreCase))
            return;

        await ClickLanguageButton();
        await ClickLanguage("English");
    }

    /// <summary>The code on the language button ("EN" or "IT"); null when the screen has no language button.</summary>
    public async Task<string?> LanguageCode() =>
        await LanguageButton.CountAsync() == 0 ? null : (await LanguageButton.InnerTextAsync()).Trim().ToUpperInvariant();

    // ----- State -----

    private ILocator Bar => Page.Locator("header.app-header");

    /// <summary>The avatar button of a user; its text is the initials, for example "DU".</summary>
    public ILocator AvatarButton(string userName) => Bar.GetByRole(AriaRole.Button, new() { Name = userName, Exact = true });

    /// <summary>"Settings" in the avatar menu; only Admins have it.</summary>
    public ILocator SettingsInMenu => Page.GetByRole(AriaRole.Button, new() { Name = "Settings", Exact = true });

    /// <summary>"Logout" in the avatar menu.</summary>
    public ILocator LogoutInMenu => Page.GetByTestId("btn-logout");

    /// <summary>The language button; its text is the code of the language, for example "EN".</summary>
    public ILocator LanguageButton => Page.GetByTestId("btn-language-selector");

    /// <summary>The open language menu.</summary>
    public ILocator LanguageMenu =>
        Page.Locator("ul.dropdown-menu").Filter(new() { Has = Page.GetByRole(AriaRole.Button, new() { Name = "Italiano", Exact = true }) });

    /// <summary>The languages the open menu offers, in order.</summary>
    public ILocator LanguageOptions => LanguageMenu.GetByRole(AriaRole.Button);

    /// <summary>The name of an organization as a button: the switcher shown only to members of several.</summary>
    public ILocator OrganizationSwitcher(string organizationName) =>
        Bar.GetByRole(AriaRole.Button, new() { Name = organizationName, Exact = true });
}
