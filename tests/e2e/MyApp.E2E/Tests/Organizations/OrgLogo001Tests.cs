using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-LOGO-001: Upload an image as the organization logo
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-LOGO-001")]
[Property("UserStory", "US-ORG-03")]
public class OrgLogo001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An admin uploads an image and sees it as the logo of the organization.")]
    public async Task UploadingAnImageShowsItAsTheLogo()
    {
        var menu = new SidebarPage(Page);
        var org = new OrganizationPage(Page);
        var edit = new UpdateOrganizationPage(Page);

        // The page has no way to remove a logo, so "MyApp Dev Org" keeps this logo until the seed runs again.
        // A repeat run still passes: it replaces the logo with the same file.

        // 1. Click the "Settings" link in the menu.
        await menu.ClickSettings();
        // 2. Click the "Edit" button.
        await org.ClickEdit();
        // 3. Upload the file "logo.png" with the "Click to upload logo" button.
        await edit.UploadLogo(TestFiles.LogoPng);

        // The message "Logo uploaded successfully" appears and the logo replaces the placeholder.
        await Expect(edit.Message("Logo uploaded successfully")).ToBeVisibleAsync();
        await Expect(edit.Logo).ToBeVisibleAsync();

        // 4. Click the "Save Changes" button.
        await edit.ClickSaveChanges();

        // After saving, the Organization page shows the logo instead of the first two letters of the name.
        await Expect(org.Logo(TestUsers.OrgDevName)).ToBeVisibleAsync();
        await Expect(org.LogoPlaceholder("MY")).ToHaveCountAsync(0);
    }
}
