using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// AUD-DETAIL-002: A created task shows only new values
/// </summary>
[TestFixture]
[Category("audit")]
[Property("TestCase", "AUD-DETAIL-002")]
[Property("UserStory", "US-AUD-03")]
public class AudDetail002Tests : AuditTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("The dialog of a created task has new values and no old values.")]
    public async Task ACreatedTaskShowsOnlyNewValues()
    {
        var title = UniqueTitle("Details check");
        var tasks = new TasksPage(Page);
        var sidebar = new SidebarPage(Page);
        var audit = new AuditLogPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type "Details check" in the "Title" field.
        await tasks.TypeTitle(title);
        // 4. Click the "Create Task" button.
        await tasks.ClickCreateTask();
        // 5. Click the "Audit Log" link in the menu.
        await sidebar.ClickAuditLog();
        // 6. Click the "Details" button on the first row of the table.
        await audit.ClickDetailsOfFirstRow();

        // The "Details" dialog shows the action "Create".
        await Expect(audit.DetailOf("Action")).ToHaveTextAsync("Create");
        // "New Values" shows "Title" with "Details check" and "Status" with "Todo".
        await Expect(audit.ValueIn(audit.NewValues, "Title")).ToHaveTextAsync(title);
        await Expect(audit.ValueIn(audit.NewValues, "Status")).ToHaveTextAsync("Todo");
        // "Old Values" is not shown.
        await Expect(audit.OldValues).ToHaveCountAsync(0);
    }
}
