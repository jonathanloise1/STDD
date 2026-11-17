using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// AUD-LIST-004: Next and Previous move between pages of 25 changes
/// </summary>
[TestFixture]
[Category("audit")]
[Property("TestCase", "AUD-LIST-004")]
[Property("UserStory", "US-AUD-01")]
public class AudList004Tests : AuditTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Next shows page 2, Previous returns to page 1 with 25 rows.")]
    public async Task NextAndPreviousMoveBetweenPagesOf25Changes()
    {
        var sidebar = new SidebarPage(Page);
        var audit = new AuditLogPage(Page);

        // 1. Click the "Audit Log" link in the menu.
        await sidebar.ClickAuditLog();
        await Expect(audit.Rows).ToHaveCountAsync(25);
        var firstPage = await audit.RowTexts();
        // 2. Click the "Next" button.
        await audit.ClickNext();

        // After step 2, the page position starts with "2 /" and the table shows the next changes.
        await Expect(audit.PagePosition(2)).ToBeVisibleAsync();
        await Expect(audit.FirstRow).ToBeVisibleAsync();
        await Expect(audit.Rows).Not.ToHaveTextAsync(firstPage);

        // 3. Click the "Previous" button.
        await audit.ClickPrevious();

        // After step 3, the page position starts with "1 /" and the table shows 25 rows.
        await Expect(audit.PagePosition(1)).ToBeVisibleAsync();
        await Expect(audit.Rows).ToHaveCountAsync(25);
    }
}
