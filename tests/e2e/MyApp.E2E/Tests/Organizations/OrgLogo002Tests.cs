using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-LOGO-002: A file that is not an image is refused
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-LOGO-002")]
[Property("UserStory", "US-ORG-03")]
public class OrgLogo002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("A text file chosen as logo is refused with a message.")]
    public async Task ATextFileIsRefused()
    {
        var menu = new SidebarPage(Page);
        var org = new OrganizationPage(Page);
        var edit = new UpdateOrganizationPage(Page);

        // 1. Click the "Settings" link in the menu.
        await menu.ClickSettings();
        // 2. Click the "Edit" button.
        await org.ClickEdit();
        // Before the upload: whether a logo is shown, to see afterwards that it did not change.
        var hadLogo = await edit.HasLogo();
        // 3. Upload the file with the "Click to upload logo" button.
        await edit.UploadLogo(TestFiles.NotesTxt);

        // The message "Only image files are allowed" appears.
        await Expect(edit.Message("Only image files are allowed")).ToBeVisibleAsync();
        // The logo does not change.
        await Expect(edit.Logo).ToHaveCountAsync(hadLogo ? 1 : 0);
    }
}
