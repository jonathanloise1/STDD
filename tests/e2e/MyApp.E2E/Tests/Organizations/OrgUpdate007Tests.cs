using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-UPDATE-007: Saving when the service does not answer shows a message
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-UPDATE-007")]
[Property("UserStory", "US-ORG-02")]
public class OrgUpdate007Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("When the service does not answer while the details are saved, the admin is told so and the page keeps the values typed.")]
    public async Task SavingWhenTheServiceDoesNotAnswerShowsAMessage()
    {
        var menu = new SidebarPage(Page);
        var org = new OrganizationPage(Page);
        var edit = new UpdateOrganizationPage(Page);

        // The service does not answer when the organization details are saved.
        ServiceDoesNotAnswer();

        // 1. Click the "Settings" link in the menu.
        await menu.ClickSettings();
        // 2. Click the "Edit" button.
        await org.ClickEdit();
        // 3. Clear the "City" field.
        await edit.ClearField("City");
        // 4. Type "Bergamo" in the "City" field.
        await edit.TypeInField("City", "Bergamo");
        // 5. Click the "Save Changes" button.
        await edit.ClickSaveChanges();

        // The message "The service did not answer. Try again in a few minutes." appears.
        await Expect(edit.Message("The service did not answer. Try again in a few minutes.")).ToBeVisibleAsync();
        // The Update Organization page stays open and the "City" field still shows "Bergamo".
        await edit.ExpectOpen();
        await Expect(edit.FormField("City")).ToHaveValueAsync("Bergamo");
    }
}
