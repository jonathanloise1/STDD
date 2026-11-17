using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-UPDATE-003: Saving with empty required fields shows field messages
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-UPDATE-003")]
[Property("UserStory", "US-ORG-02")]
public class OrgUpdate003Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Clearing required fields and saving keeps the page open and shows a message under each cleared field.")]
    public async Task SavingWithEmptyRequiredFieldsShowsFieldMessages()
    {
        var menu = new SidebarPage(Page);
        var org = new OrganizationPage(Page);
        var edit = new UpdateOrganizationPage(Page);

        // 1. Click the "Settings" link in the menu.
        await menu.ClickSettings();
        // 2. Click the "Edit" button.
        await org.ClickEdit();
        // 3. Clear the "Organization Name" field.
        await edit.ClearField("Organization Name");
        // 4. Clear the "Legal Name" field.
        await edit.ClearField("Legal Name");
        // 5. Click the "Save Changes" button.
        await edit.ClickSaveChanges();

        // The "Organization Name" field shows "Organization name is required".
        await Expect(edit.FieldMessage("Organization Name")).ToHaveTextAsync("Organization name is required");
        // The "Legal Name" field shows "Legal name is required".
        await Expect(edit.FieldMessage("Legal Name")).ToHaveTextAsync("Legal name is required");
        // The message "Please fix validation errors before submitting" appears.
        await Expect(edit.Message("Please fix validation errors before submitting")).ToBeVisibleAsync();
        // The Update Organization page stays open.
        await edit.ExpectOpen();
    }
}
