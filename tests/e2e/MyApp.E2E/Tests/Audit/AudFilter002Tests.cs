using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// AUD-FILTER-002: A date range without changes shows an empty table
/// </summary>
[TestFixture]
[Category("audit")]
[Property("TestCase", "AUD-FILTER-002")]
[Property("UserStory", "US-AUD-02")]
public class AudFilter002Tests : AuditTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("A date in the future with the type Task leaves an empty table.")]
    public async Task ADateRangeWithoutChangesShowsAnEmptyTable()
    {
        var sidebar = new SidebarPage(Page);
        var audit = new AuditLogPage(Page);

        // 1. Click the "Audit Log" link in the menu.
        await sidebar.ClickAuditLog();
        // 2. Select "Task" in the "Type" field.
        await audit.SelectType("Task");
        // 3. Type "2099-12-31" in the "From" field.
        await audit.TypeFrom("2099-12-31");

        // The table shows "No audit logs found."
        await Expect(audit.EmptyMessage).ToBeVisibleAsync();
    }
}
