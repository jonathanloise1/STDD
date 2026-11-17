using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// AUD-DETAIL-003: Cancel closes the details dialog
/// </summary>
[TestFixture]
[Category("audit")]
[Property("TestCase", "AUD-DETAIL-003")]
[Property("UserStory", "US-AUD-03")]
public class AudDetail003Tests : AuditTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Cancel closes the dialog and the table keeps its rows.")]
    public async Task CancelClosesTheDetailsDialog()
    {
        var sidebar = new SidebarPage(Page);
        var audit = new AuditLogPage(Page);

        // 1. Click the "Audit Log" link in the menu.
        await sidebar.ClickAuditLog();
        await Expect(audit.Rows).ToHaveCountAsync(25);
        var rowsBefore = await audit.RowTexts();
        // 2. Click the "Details" button on the first row of the table.
        await audit.ClickDetailsOfFirstRow();
        await Expect(audit.DetailsDialog).ToBeVisibleAsync();
        // 3. Click the "Cancel" button.
        await audit.ClickCancelInDialog();

        // The "Details" dialog is closed.
        await Expect(audit.DetailsDialog).ToBeHiddenAsync();
        // The table still shows the same rows.
        await Expect(audit.Rows).ToHaveTextAsync(rowsBefore);
    }
}
