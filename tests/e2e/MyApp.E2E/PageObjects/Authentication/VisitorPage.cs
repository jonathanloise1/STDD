using Microsoft.Playwright;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Authentication;

/// <summary>
/// The visitor's side of signing in: opening an address with no user, and signing in.
///
/// The sign-in page is the identity provider's, with its email and code fields, and E2E tests cannot drive
/// it: they sign in by impersonation (development.md 16.1). So this page object covers what the application
/// itself does around the sign-in page:
/// - a visitor sees nothing of the application (no header, no menu, no page): see <c>HeaderPage.Bar</c> and
///   <c>SidebarPage.Menu</c>, which must not exist;
/// - after the sign-in, the page the visitor asked for opens.
/// What the sign-in page itself shows (the email field, no password field, no other service) is not covered.
/// </summary>
public sealed class VisitorPage : BasePage
{
    private string _requested = "/";

    public VisitorPage(IPage page) : base(page) { }

    // ----- Actions -----

    /// <summary>"Open the home page." As a visitor: the application asks to sign in.</summary>
    public Task OpenHomePage() => OpenAsVisitor("/");

    /// <summary>"Open the Tasks page." As a visitor: the application asks to sign in.</summary>
    public Task OpenTasksPage() => OpenAsVisitor("/tasks");

    /// <summary>"Open the Set Up Your Organization page." As a visitor: the application asks to sign in.</summary>
    public Task OpenSetupPage() => OpenAsVisitor("/onboarding");

    /// <summary>
    /// "Sign in as {user}." The session was made for this user. Completing the identity provider's sign-in
    /// is replaced by making the session signed in, and the page the visitor asked for opens, as it does when
    /// the identity provider returns to the application. Returns when the application is showing.
    /// </summary>
    public async Task SignInAs(TestUser user)
    {
        LogStep("Sign in as", user.Email);
        var sessionUser = await E2EImpersonation.ResumeSessionAsync(Page.Context);
        if (!string.Equals(sessionUser, user.Email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"The session was made for {sessionUser}; an E2E session can only sign in as that user, not as {user.Email}.");

        await Page.GotoAsync($"{TestConfiguration.BaseUrl}{_requested}");
        await WaitForPageLoadAsync();
        await new AddressPage(Page).WaitUntilAppIsShown();
    }

    // ----- State -----

    /// <summary>A password field, wherever the browser is.</summary>
    public ILocator PasswordField => Page.GetByLabel(new System.Text.RegularExpressions.Regex("password", System.Text.RegularExpressions.RegexOptions.IgnoreCase));

    /// <summary>A registration form of the application: its "Create Account" or "Sign Up" heading.</summary>
    public ILocator RegistrationForm =>
        Page.GetByText(new System.Text.RegularExpressions.Regex("Create Account|Sign Up", System.Text.RegularExpressions.RegexOptions.IgnoreCase));

    private async Task OpenAsVisitor(string path)
    {
        LogStep("Open as a visitor", path);
        _requested = path;
        await Page.GotoAsync($"{TestConfiguration.BaseUrl}{path}");
        await WaitForPageLoadAsync();
    }
}
