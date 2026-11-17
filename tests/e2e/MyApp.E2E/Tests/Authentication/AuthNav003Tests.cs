using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Authentication;

/// <summary>
/// AUTH-NAV-003: An unknown address shows Page not found
/// </summary>
[TestFixture]
[Category("authentication")]
[Property("TestCase", "AUTH-NAV-003")]
[Property("UserStory", "US-AUTH-06")]
public class AuthNav003Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("A signed-in user who opens an address that matches no page is told so and can go back to the app.")]
    public async Task AnUnknownAddressShowsPageNotFound()
    {
        var menu = new SidebarPage(Page);
        var notFound = new NotFoundPage(Page);

        // 1. Open the "Does not exist" page.
        await notFound.Open();
        // After step 1, "Page not found" is shown, with a "Homepage" link.
        await Expect(notFound.Title).ToBeVisibleAsync();
        await Expect(notFound.HomepageLink).ToBeVisibleAsync();
        // 2. Click the "Homepage" link.
        await notFound.ClickHomepage();
        // After step 2, the Dashboard page opens.
        await Expect(menu.Entry("Dashboard")).ToHaveAttributeAsync("aria-current", "page");
    }
}
