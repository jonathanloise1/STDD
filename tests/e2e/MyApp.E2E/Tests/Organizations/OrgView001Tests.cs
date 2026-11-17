using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-VIEW-001: Settings shows the profile and billing details
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-VIEW-001")]
[Property("UserStory", "US-ORG-01")]
public class OrgView001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An admin opens Settings and sees the details of the organization.")]
    public async Task SettingsShowsTheProfileAndBillingDetails()
    {
        var menu = new SidebarPage(Page);
        var org = new OrganizationPage(Page);

        // 1. Click the "Settings" link in the menu.
        await menu.ClickSettings();

        // The Organization page opens with the title "Billing Information" and the name "MyApp Dev Org".
        await Expect(org.Title).ToBeVisibleAsync();
        await Expect(org.OrganizationName(TestUsers.OrgDevName)).ToBeVisibleAsync();
        // "VAT Number" shows "IT00000000000" and "Fiscal Code" shows "00000000000".
        await Expect(org.Detail("VAT Number")).ToHaveTextAsync("IT00000000000");
        await Expect(org.Detail("Fiscal Code")).ToHaveTextAsync("00000000000");
        // "Email" shows "dev@myapp.local" and "Address" shows "Via dello Sviluppo 1".
        await Expect(org.Detail("Email")).ToHaveTextAsync("dev@myapp.local");
        await Expect(org.Detail("Address")).ToHaveTextAsync("Via dello Sviluppo 1");
        // "City" shows "Milano (MI)", "ZIP Code" shows "20100" and "Country" shows "IT".
        await Expect(org.Detail("City")).ToHaveTextAsync("Milano (MI)");
        await Expect(org.Detail("ZIP Code")).ToHaveTextAsync("20100");
        await Expect(org.Detail("Country")).ToHaveTextAsync("IT");
        // An "Edit" button is shown.
        await Expect(org.EditButton).ToBeVisibleAsync();
    }
}
