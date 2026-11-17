using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// AUD-LIST-002: A new task appears at the top of the list
/// </summary>
[TestFixture]
[Category("audit")]
[Property("TestCase", "AUD-LIST-002")]
[Property("UserStory", "US-AUD-01")]
public class AudList002Tests : AuditTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("The change of a new task is the first row, made by Dev User.")]
    public async Task ANewTaskAppearsAtTheTopOfTheList()
    {
        var title = UniqueTitle("Audit trail check");
        var tasks = new TasksPage(Page);
        var sidebar = new SidebarPage(Page);
        var audit = new AuditLogPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type "Audit trail check" in the "Title" field.
        await tasks.TypeTitle(title);
        // 4. Click the "Create Task" button.
        await tasks.ClickCreateTask();
        // 5. Click the "Audit Log" link in the menu.
        await sidebar.ClickAuditLog();

        // The first row of the table shows the action "Create" and the type "Task".
        await Expect(audit.ActionOf(audit.FirstRow)).ToHaveTextAsync("Create");
        await Expect(audit.TypeOf(audit.FirstRow)).ToHaveTextAsync("Task");
        // The "User" column of the first row shows "Dev User".
        await Expect(audit.UserOf(audit.FirstRow)).ToHaveTextAsync("Dev User");
        // The "Changes" column of the first row shows "-".
        await Expect(audit.ChangesOf(audit.FirstRow)).ToContainTextAsync("-");
    }
}
