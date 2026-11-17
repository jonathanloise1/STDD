using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;

namespace MyApp.E2E.Tests.Organizations;

/// <summary>
/// ORG-UPDATE-006: The Unsaved Changes badge follows the edited values
/// </summary>
[TestFixture]
[Category("organizations")]
[Property("TestCase", "ORG-UPDATE-006")]
[Property("UserStory", "US-ORG-02")]
public class OrgUpdate006Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("The badge appears when a value differs from the saved one and disappears when the original value is typed back.")]
    public async Task TheBadgeFollowsTheEditedValues()
    {
        var menu = new SidebarPage(Page);
        var org = new OrganizationPage(Page);
        var edit = new UpdateOrganizationPage(Page);

        // 1. Click the "Settings" link in the menu.
        await menu.ClickSettings();
        // 2. Click the "Edit" button.
        await org.ClickEdit();
        // 3. Clear the "Legal Name" field.
        await edit.ClearField("Legal Name");
        // 4. Type "Changed Legal Name" in the "Legal Name" field.
        await edit.TypeInField("Legal Name", "Changed Legal Name");
        // After step 4 the badge "Unsaved Changes" is shown next to the title.
        await Expect(edit.UnsavedChangesBadge).ToBeVisibleAsync();
        // 5. Clear the "Legal Name" field.
        await edit.ClearField("Legal Name");
        // 6. Type "MyApp Dev Org S.r.l." in the "Legal Name" field.
        await edit.TypeInField("Legal Name", "MyApp Dev Org S.r.l.");
        // After step 6 the badge "Unsaved Changes" is no longer shown.
        await Expect(edit.UnsavedChangesBadge).ToHaveCountAsync(0);
    }
}
