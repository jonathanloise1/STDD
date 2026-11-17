using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// AUD-LIST-007: Loading the changes when the service does not answer shows a message
/// </summary>
[TestFixture]
[Category("audit")]
[Property("TestCase", "AUD-LIST-007")]
[Property("UserStory", "US-AUD-01")]
public class AudList007Tests : AuditTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("A message tells that the service did not answer instead of an empty history.")]
    public async Task LoadingTheChangesWhenTheServiceDoesNotAnswerShowsAMessage()
    {
        var sidebar = new SidebarPage(Page);
        var audit = new AuditLogPage(Page);
        ServiceDoesNotAnswer();

        // 1. Click the "Audit Log" link in the menu.
        await sidebar.ClickAuditLog();

        // The message "The service did not answer. Try again in a few minutes." appears.
        await Expect(audit.Message("The service did not answer. Try again in a few minutes.")).ToBeVisibleAsync();
        // The table does not show "No audit logs found."
        await Expect(audit.EmptyMessage).ToHaveCountAsync(0);
    }
}
