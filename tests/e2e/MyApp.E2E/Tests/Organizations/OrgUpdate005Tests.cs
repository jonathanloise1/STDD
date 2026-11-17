using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-UPDATE-005: A ZIP code that is not five digits shows a field message
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-UPDATE-005")]
[Property("UserStory", "US-ORG-02")]
public class OrgUpdate005Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("A ZIP / Postal Code that is not exactly five digits cannot be saved.")]
    public async Task AZipCodeThatIsNotFiveDigitsShowsAFieldMessage()
    {
        var menu = new SidebarPage(Page);
        var org = new OrganizationPage(Page);
        var edit = new UpdateOrganizationPage(Page);

        // 1. Click the "Settings" link in the menu.
        await menu.ClickSettings();
        // 2. Click the "Edit" button.
        await org.ClickEdit();
        // 3. Clear the "ZIP / Postal Code" field.
        await edit.ClearField("ZIP / Postal Code");
        // 4. Type "201" in the "ZIP / Postal Code" field.
        await edit.TypeInField("ZIP / Postal Code", "201");
        // 5. Click the "Save Changes" button.
        await edit.ClickSaveChanges();

        // The "ZIP / Postal Code" field shows "ZIP code must be 5 digits".
        await Expect(edit.FieldMessage("ZIP / Postal Code")).ToHaveTextAsync("ZIP code must be 5 digits");
        // The Update Organization page stays open.
        await edit.ExpectOpen();
    }
}
