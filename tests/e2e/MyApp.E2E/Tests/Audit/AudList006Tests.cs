using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Dashboard;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// AUD-LIST-006: An editor cannot open the audit trail
/// </summary>
[TestFixture]
[Category("audit")]
[Property("TestCase", "AUD-LIST-006")]
[Property("UserStory", "US-AUD-01")]
public class AudList006Tests : AuditTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("The dashboard opens with a message and the menu has no Audit Log link.")]
    public async Task AnEditorCannotOpenTheAuditTrail()
    {
        var audit = new AuditLogPage(Page);
        var dashboard = new DashboardPage(Page);
        var sidebar = new SidebarPage(Page);

        // 1. Open the Audit Log page.
        await audit.Open();

        // The Dashboard page opens with the message "You don't have access to this page."
        await Expect(dashboard.Greeting).ToBeVisibleAsync();
        await Expect(sidebar.ActiveEntry).ToHaveAccessibleNameAsync("Dashboard");
        await Expect(dashboard.Message("You don't have access to this page.")).ToBeVisibleAsync();
        // The menu has no "Audit Log" link.
        await Expect(sidebar.Entry("Audit Log")).ToHaveCountAsync(0);
    }
}
