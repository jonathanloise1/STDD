using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// AUD-FILTER-001: Choosing a type shows only changes of that type
/// </summary>
[TestFixture]
[Category("audit")]
[Property("TestCase", "AUD-FILTER-001")]
[Property("UserStory", "US-AUD-02")]
public class AudFilter001Tests : AuditTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Every row shows the type Task and the count drops.")]
    public async Task ChoosingATypeShowsOnlyChangesOfThatType()
    {
        var sidebar = new SidebarPage(Page);
        var audit = new AuditLogPage(Page);

        // 1. Click the "Audit Log" link in the menu.
        await sidebar.ClickAuditLog();
        var before = await audit.TotalCount();
        // 2. Select "Task" in the "Type" field.
        await audit.SelectType("Task");

        // Every row of the table shows the type "Task".
        await Expect(audit.FirstRow).ToBeVisibleAsync();
        await Expect(audit.RowsNotOfType("Task")).ToHaveCountAsync(0);
        // The count above the table is lower than before.
        var after = await audit.TotalCount();
        Assert.That(after, Is.LessThan(before));
    }
}
