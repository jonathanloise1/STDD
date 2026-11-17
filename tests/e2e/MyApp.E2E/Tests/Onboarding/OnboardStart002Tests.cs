using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Onboarding;

namespace MyApp.E2E.Tests.Onboarding;

/// <summary>
/// ONBOARD-START-002: A member of an organization is not sent to the setup page
/// </summary>
[TestFixture]
[Category("onboarding")]
[Property("TestCase", "ONBOARD-START-002")]
[Property("UserStory", "US-ONBOARD-01")]
public class OnboardStart002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("A user who belongs to an organization reaches the dashboard directly.")]
    public async Task AMemberOfAnOrganizationIsNotSentToTheSetupPage()
    {
        var address = new AddressPage(Page);
        var menu = new SidebarPage(Page);
        var setup = new SetupPage(Page);

        // 1. Open the Dashboard page.
        await address.OpenDashboardPage();
        // The Dashboard page opens.
        await Expect(menu.Entry("Dashboard")).ToHaveAttributeAsync("aria-current", "page");
        // The text "Set Up Your Organization" is not shown.
        await Expect(setup.Title).ToHaveCountAsync(0);
    }
}
