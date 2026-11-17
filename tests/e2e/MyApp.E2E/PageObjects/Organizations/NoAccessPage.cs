using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Organizations;

/// <summary>
/// Where a user who opens a page of the Admins lands: the Dashboard with the message "You don't have access
/// to this page.". Only what the organizations tests need of the Dashboard.
/// </summary>
public sealed class NoAccessPage : BasePage
{
    public NoAccessPage(IPage page) : base(page) { }

    /// <summary>"The Dashboard page opens": the address ends with "/dashboard".</summary>
    public Task ExpectDashboardOpen() =>
        Assertions.Expect(Page).ToHaveURLAsync(new Regex("/dashboard$"));

    /// <summary>The message "You don't have access to this page.".</summary>
    public ILocator NoAccessMessage => Message("You don't have access to this page.");
}
