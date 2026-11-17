using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-VIEW-003: Loading the details when the service does not answer shows a message
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-VIEW-003")]
[Property("UserStory", "US-ORG-01")]
public class OrgView003Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("When the service does not answer while the organization details are loaded, the admin is told so.")]
    public async Task LoadingTheDetailsWhenTheServiceDoesNotAnswerShowsAMessage()
    {
        var menu = new SidebarPage(Page);
        var org = new OrganizationPage(Page);

        // The service does not answer when the organization details are loaded.
        ServiceDoesNotAnswer();

        // 1. Click the "Settings" link in the menu.
        await menu.ClickSettings();

        // The message "The service did not answer. Try again in a few minutes." appears.
        await Expect(org.Message("The service did not answer. Try again in a few minutes.")).ToBeVisibleAsync();
        // The page does not show "VAT Number".
        await Expect(org.Detail("VAT Number")).ToHaveCountAsync(0);
    }
}
