using Microsoft.Playwright;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.PageObjects.Common;

/// <summary>
/// The sign-in page. It belongs to the identity provider, not to the application: a visitor who is not signed
/// in is sent there, so "the sign-in page opens" means the browser has left the application.
/// </summary>
public sealed class SignInPage : BasePage
{
    public SignInPage(IPage page) : base(page) { }

    /// <summary>
    /// "The sign-in page opens": waits until the browser is on the identity provider's address, not merely away
    /// from the application (a browser error page is away too).
    /// </summary>
    public Task ExpectOpen() =>
        Page.WaitForURLAsync(url => new Uri(url).Host.Equals(IdentityProviderStandIn.Host, StringComparison.OrdinalIgnoreCase));
}
