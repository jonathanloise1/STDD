using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-UPDATE-004: An invalid billing email shows a field message
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-UPDATE-004")]
[Property("UserStory", "US-ORG-02")]
public class OrgUpdate004Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("A Billing Email that is not a valid address cannot be saved.")]
    public async Task AnInvalidBillingEmailShowsAFieldMessage()
    {
        var menu = new SidebarPage(Page);
        var org = new OrganizationPage(Page);
        var edit = new UpdateOrganizationPage(Page);

        // 1. Click the "Settings" link in the menu.
        await menu.ClickSettings();
        // 2. Click the "Edit" button.
        await org.ClickEdit();
        // 3. Clear the "Billing Email" field.
        await edit.ClearField("Billing Email");
        // 4. Type "billing-at-example" in the "Billing Email" field.
        await edit.TypeInField("Billing Email", "billing-at-example");
        // 5. Click the "Save Changes" button.
        await edit.ClickSaveChanges();

        // The "Billing Email" field shows "Invalid email format".
        await Expect(edit.FieldMessage("Billing Email")).ToHaveTextAsync("Invalid email format");
        // The Update Organization page stays open.
        await edit.ExpectOpen();
    }
}
