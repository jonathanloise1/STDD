using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;
using MyApp.E2E.PageObjects.Common;
using System.Text.RegularExpressions;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// AUD-LIST-001: The Audit Log page lists the changes of the organization
/// </summary>
[TestFixture]
[Category("audit")]
[Property("TestCase", "AUD-LIST-001")]
[Property("UserStory", "US-AUD-01")]
public class AudList001Tests : AuditTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("The page shows the columns, 25 rows and the total count.")]
    public async Task TheAuditLogPageListsTheChangesOfTheOrganization()
    {
        var sidebar = new SidebarPage(Page);
        var audit = new AuditLogPage(Page);

        // 1. Click the "Audit Log" link in the menu.
        await sidebar.ClickAuditLog();

        // The Audit Log page opens.
        await Expect(audit.Heading).ToBeVisibleAsync();
        // The table shows the columns "Date/Time", "Action", "Type", "User" and "Changes".
        await Expect(audit.ColumnHeaders).ToHaveTextAsync(new[] { "Date/Time", "Action", "Type", "User", "Changes" });
        // The table shows 25 rows.
        await Expect(audit.Rows).ToHaveCountAsync(25);
        // A count above the table shows the total number of changes.
        await Expect(audit.Total).ToHaveTextAsync(new Regex(@"^\d+ changes$"));
    }
}
