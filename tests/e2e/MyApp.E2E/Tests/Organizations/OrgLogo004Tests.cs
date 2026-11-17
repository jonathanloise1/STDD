using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-LOGO-004: Uploading when the service does not answer shows a message
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-LOGO-004")]
[Property("UserStory", "US-ORG-03")]
public class OrgLogo004Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("When the service does not answer while the logo is uploaded, the admin is told so and the logo does not change.")]
    public async Task UploadingWhenTheServiceDoesNotAnswerShowsAMessage()
    {
        var menu = new SidebarPage(Page);
        var org = new OrganizationPage(Page);
        var edit = new UpdateOrganizationPage(Page);

        // The service does not answer when a logo is uploaded.
        ServiceDoesNotAnswer();

        // 1. Click the "Settings" link in the menu.
        await menu.ClickSettings();
        // 2. Click the "Edit" button.
        await org.ClickEdit();
        // Before the upload: whether a logo is shown, to see afterwards that it did not change.
        var hadLogo = await edit.HasLogo();
        // 3. Upload the file "logo.png" with the "Click to upload logo" button.
        await edit.UploadLogo(TestFiles.LogoPng);

        // The message "The service did not answer. Try again in a few minutes." appears.
        await Expect(edit.Message("The service did not answer. Try again in a few minutes.")).ToBeVisibleAsync();
        // The logo does not change.
        await Expect(edit.Logo).ToHaveCountAsync(hadLogo ? 1 : 0);
    }
}
