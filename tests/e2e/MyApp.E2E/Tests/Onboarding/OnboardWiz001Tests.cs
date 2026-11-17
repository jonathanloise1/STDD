using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Onboarding;

namespace MyApp.E2E.Tests.Onboarding;

/// <summary>
/// ONBOARD-WIZ-001: Submitting the details creates the organization
/// </summary>
[TestFixture]
[Category("onboarding")]
[Property("TestCase", "ONBOARD-WIZ-001")]
[Property("UserStory", "US-ONBOARD-02")]
public class OnboardWiz001Tests : VisitorTestBase
{
    protected override TestUser GetTestUser() => TestUsers.SetupUser;

    [Test]
    [Description("A user without an organization fills in the wizard, the organization is created and its dashboard opens with the user as Admin.")]
    public async Task SubmittingTheDetailsCreatesTheOrganization()
    {
        var visitor = new VisitorPage(Page);
        var setup = new SetupPage(Page);
        var menu = new SidebarPage(Page);
        var header = new HeaderPage(Page);
        var address = new AddressPage(Page);

        // 1. Open the home page.
        await visitor.OpenHomePage();
        // 2. Sign in as "Setup User".
        await visitor.SignInAs(TestUsers.SetupUser);
        // 3. Type "Globex Corp" in the "Organization Name" field.
        await setup.TypeOrganizationName("Globex Corp");
        // 4. Type "Globex Corporation S.r.l." in the "Legal Name" field.
        await setup.TypeLegalName("Globex Corporation S.r.l.");
        // 5. Type "IT01234567890" in the "VAT Number" field.
        await setup.TypeVatNumber("IT01234567890");
        // 6. Type "billing@globex.test" in the "Billing Email" field.
        await setup.TypeBillingEmail("billing@globex.test");
        // 7. Click the "Submit" button.
        await setup.ClickSubmit();
        // The message "Organization created successfully!" appears.
        await Expect(address.Message("Organization created successfully!")).ToBeVisibleAsync();
        // The Dashboard page opens and the header shows "Globex Corp".
        await Expect(menu.Entry("Dashboard")).ToHaveAttributeAsync("aria-current", "page");
        await Expect(header.OrganizationName("Globex Corp")).ToBeVisibleAsync();
        // The menu shows "Settings".
        await Expect(menu.Entry("Settings")).ToBeVisibleAsync();
    }
}
