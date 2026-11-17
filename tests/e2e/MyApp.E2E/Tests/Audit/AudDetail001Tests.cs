using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Audit;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Audit;

/// <summary>
/// AUD-DETAIL-001: An edited task shows its old and new title
/// </summary>
[TestFixture]
[Category("audit")]
[Property("TestCase", "AUD-DETAIL-001")]
[Property("UserStory", "US-AUD-03")]
public class AudDetail001Tests : AuditTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    [Test]
    [Description("The dialog of a renamed task shows the old and the new title.")]
    public async Task AnEditedTaskShowsItsOldAndNewTitle()
    {
        var oldTitle = UniqueTitle("Audit rename check");
        var newTitle = Keep(oldTitle + " Q4");
        var tasks = new TasksPage(Page);
        var sidebar = new SidebarPage(Page);
        var audit = new AuditLogPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type "Audit rename check" in the "Title" field.
        await tasks.TypeTitle(oldTitle);
        // 4. Click the "Create Task" button.
        await tasks.ClickCreateTask();
        // 5. Click the "Edit" button on the row of "Audit rename check".
        await tasks.ClickEdit(oldTitle);
        // 6. Clear the "Title" field.
        await tasks.ClearTitle();
        // 7. Type "Audit rename check Q4" in the "Title" field.
        await tasks.TypeTitle(newTitle);
        // 8. Click the "Update" button.
        await tasks.ClickUpdate();
        // 9. Click the "Audit Log" link in the menu.
        await sidebar.ClickAuditLog();

        // The "Changes" column of the first row lists "Title".
        await Expect(audit.ChangesOf(audit.FirstRow)).ToContainTextAsync("Title");

        // 10. Click the "Details" button on the first row of the table.
        await audit.ClickDetailsOfFirstRow();

        // The "Details" dialog shows the action "Update", the type "Task" and the user "Dev User".
        await Expect(audit.DetailOf("Action")).ToHaveTextAsync("Update");
        await Expect(audit.DetailOf("Type")).ToHaveTextAsync("Task");
        await Expect(audit.DetailOf("User")).ToHaveTextAsync("Dev User");
        // "Old Values" shows "Title" with "Audit rename check".
        await Expect(audit.ValueIn(audit.OldValues, "Title")).ToHaveTextAsync(oldTitle);
        // "New Values" shows "Title" with "Audit rename check Q4".
        await Expect(audit.ValueIn(audit.NewValues, "Title")).ToHaveTextAsync(newTitle);
    }
}
