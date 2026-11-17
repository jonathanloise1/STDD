using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-ACCESS-004: A non-admin opening the Audit Log returns to the Dashboard
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-ACCESS-004")]
[Property("UserStory", "US-AUTH-07")]
public class AuthAccess004Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("The role in the current organization decides: an Editor who opens the Audit Log page by its address is taken back to the Dashboard page and told why.")]
    public async Task ANonAdminOpeningTheAuditLogReturnsToTheDashboard()
    {
        var address = new AddressPage(Page);
        var menu = new SidebarPage(Page);

        // 1. Open the Audit Log page.
        await address.OpenAuditLogPage();
        // The Dashboard page opens with the message "You don't have access to this page."
        await Expect(menu.Entry("Dashboard")).ToHaveAttributeAsync("aria-current", "page");
        await Expect(address.Message("You don't have access to this page.")).ToBeVisibleAsync();
        // The Audit Log page is not shown.
        await Expect(menu.Entry("Audit Log")).ToHaveCountAsync(0);
    }
}
