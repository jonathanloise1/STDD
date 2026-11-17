using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// AUD-LIST-003: An organization without changes shows an empty message
/// </summary>
[TestFixture]
[Category("audit")]
[Property("TestCase", "AUD-LIST-003")]
[Property("UserStory", "US-AUD-01")]
public class AudList003Tests : AuditTestBase
{
    protected override TestUser GetTestUser() => TestUsers.EmptyAdmin;

    [Test]
    [Description("The table says there are no audit logs and offers no paging.")]
    public async Task AnOrganizationWithoutChangesShowsAnEmptyMessage()
    {
        var sidebar = new SidebarPage(Page);
        var audit = new AuditLogPage(Page);

        // 1. Click the "Audit Log" link in the menu.
        await sidebar.ClickAuditLog();

        // The table shows "No audit logs found."
        await Expect(audit.EmptyMessage).ToBeVisibleAsync();
        // No "Previous" or "Next" button is shown.
        await Expect(audit.PreviousButton).ToHaveCountAsync(0);
        await Expect(audit.NextButton).ToHaveCountAsync(0);
    }
}
