using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// AUD-FILTER-004: The Type field keeps every choice after filtering
/// </summary>
[TestFixture]
[Category("audit")]
[Property("TestCase", "AUD-FILTER-004")]
[Property("UserStory", "US-AUD-02")]
public class AudFilter004Tests : AuditTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("After filtering by Task the field still offers every type, and Team member can be chosen next.")]
    public async Task TheTypeFieldKeepsEveryChoiceAfterFiltering()
    {
        var sidebar = new SidebarPage(Page);
        var audit = new AuditLogPage(Page);

        // 1. Click the "Audit Log" link in the menu.
        await sidebar.ClickAuditLog();
        // 2. Select "Task" in the "Type" field.
        await audit.SelectType("Task");

        // After step 2, the "Type" field still offers "All types", "Task", "Team member", "Organization" and "User".
        await Expect(audit.TypeChoices).ToHaveTextAsync(new[] { "All types", "Task", "Team member", "Organization", "User" });

        // 3. Select "Team member" in the "Type" field.
        await audit.SelectType("Team member");

        // After step 3, every row of the table shows the type "Team member".
        await Expect(audit.FirstRow).ToBeVisibleAsync();
        await Expect(audit.RowsNotOfType("Team member")).ToHaveCountAsync(0);
    }
}
