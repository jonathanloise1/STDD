using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-UPDATE-001: Edit the city of the organization
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-UPDATE-001")]
[Property("UserStory", "US-ORG-02")]
public class OrgUpdate001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An admin changes the city of the organization and sees the new value on the Organization page.")]
    public async Task EditingTheCityShowsTheNewValueOnTheOrganizationPage()
    {
        var menu = new SidebarPage(Page);
        var org = new OrganizationPage(Page);
        var edit = new UpdateOrganizationPage(Page);

        // 1. Click the "Settings" link in the menu.
        await menu.ClickSettings();
        // 2. Click the "Edit" button.
        await org.ClickEdit();
        // 3. Clear the "City" field.
        await edit.ClearField("City");
        // 4. Type "Torino" in the "City" field.
        await edit.TypeInField("City", "Torino");
        // 5. Click the "Save Changes" button.
        await edit.ClickSaveChanges();

        // After the first save, the message "Organization has been updated successfully" appears and the Organization page opens.
        await Expect(org.Message("Organization has been updated successfully")).ToBeVisibleAsync();
        await Expect(org.Title).ToBeVisibleAsync();
        // After the first save, "City" shows "Torino (MI)".
        await Expect(org.Detail("City")).ToHaveTextAsync("Torino (MI)");

        // 6. Click the "Edit" button.
        await org.ClickEdit();
        // 7. Clear the "City" field.
        await edit.ClearField("City");
        // 8. Type "Milano" in the "City" field.
        await edit.TypeInField("City", "Milano");
        // 9. Click the "Save Changes" button.
        await edit.ClickSaveChanges();

        // After the second save, "City" shows "Milano (MI)" again.
        await Expect(org.Detail("City")).ToHaveTextAsync("Milano (MI)");
    }

    /// <summary>
    /// The test restores the city in its own steps; this does the same when a failure stopped it half way,
    /// so the next test still finds "Milano" (development.md 10.3). Best effort.
    /// </summary>
    protected override async Task CleanUpAsync()
    {
        try
        {
            var edit = new UpdateOrganizationPage(Page);
            await edit.Open();
            if (await edit.FormField("City").InputValueAsync() != "Milano")
            {
                await edit.ClearField("City");
                await edit.TypeInField("City", "Milano");
                await edit.ClickSaveChanges();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[E2E] Clean-up of the city failed: {ex.Message}");
        }
    }
}
