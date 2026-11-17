using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-LOGO-003: An image larger than 5 MB is refused
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-LOGO-003")]
[Property("UserStory", "US-ORG-03")]
public class OrgLogo003Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("An image bigger than 5 MB chosen as logo is refused with a message.")]
    public async Task AnImageLargerThan5MbIsRefused()
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
        await edit.UploadLogo(TestFiles.LargePhotoPng());

        // The message "Maximum file size is 5MB" appears.
        await Expect(edit.Message("Maximum file size is 5MB")).ToBeVisibleAsync();
        // The logo does not change.
        await Expect(edit.Logo).ToHaveCountAsync(hadLogo ? 1 : 0);
    }
}
