using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// AUD-FILTER-003: Reset clears all filters
/// </summary>
[TestFixture]
[Category("audit")]
[Property("TestCase", "AUD-FILTER-003")]
[Property("UserStory", "US-AUD-02")]
public class AudFilter003Tests : AuditTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("Reset returns the filters to their starting values and lists the changes again.")]
    public async Task ResetClearsAllFilters()
    {
        var sidebar = new SidebarPage(Page);
        var audit = new AuditLogPage(Page);

        // 1. Click the "Audit Log" link in the menu.
        await sidebar.ClickAuditLog();
        // 2. Type "2099-12-31" in the "From" field.
        await audit.TypeFrom("2099-12-31");
        await Expect(audit.EmptyMessage).ToBeVisibleAsync();
        // 3. Click the "Reset" button.
        await audit.ClickReset();

        // The "Type" field shows "All types".
        await Expect(audit.TypeChoice).ToHaveTextAsync("All types");
        // The "From" and "To" fields are empty.
        await Expect(audit.FromField).ToHaveValueAsync("");
        await Expect(audit.ToField).ToHaveValueAsync("");
        // The table lists the changes of "MyApp Dev Org" again.
        await Expect(audit.Rows).ToHaveCountAsync(25);
    }
}
