using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-EDIT-007: Updating after the role became Viewer shows a message
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-EDIT-007")]
[Property("UserStory", "US-TASK-02")]
public class TaskEdit007Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("An editor whose role is changed to Viewer while the edit form is open cannot update the task and is told why; the task keeps its title.")]
    public async Task UpdatingAfterTheRoleBecameViewerShowsAMessage()
    {
        var tasks = new TasksPage(Page);
        // A second session is signed in as Admin of "MyApp Dev Org".
        var admin = await StartSecondSession(TestUsers.AdminUser);
        var adminMenu = new SidebarPage(admin);
        var adminTeam = new TeamPage(admin);
        var adminTasks = new TasksPage(admin);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Edit" button on the row of "Review security configuration".
        await tasks.ClickEdit("Review security configuration");
        // 3. Clear the "Title" field.
        await tasks.ClearTitle();
        // 4. Type "Review the firewall rules" in the "Title" field.
        await tasks.TypeTitle("Review the firewall rules");
        // 5. As Admin: Click the "Team" link in the menu.
        await adminMenu.ClickTeam();
        // 6. As Admin: Click the "Edit" button on the row of "Dev Member".
        await adminTeam.ClickEdit(TestUsers.MemberUser.Name);
        // 7. As Admin: Check the "Viewer" field.
        await adminTeam.CheckRole("Viewer");
        // 8. As Admin: Click the "Save" button.
        await adminTeam.ClickSave();
        // 9. Click the "Update" button.
        await tasks.ClickUpdate();
        // After step 9, the message "Your role does not allow this change." appears.
        await Expect(tasks.Message("Your role does not allow this change.")).ToBeVisibleAsync();
        // 10. As Admin: Click the "Edit" button on the row of "Dev Member".
        await adminTeam.ClickEdit(TestUsers.MemberUser.Name);
        // 11. As Admin: Check the "Editor" field.
        await adminTeam.CheckRole("Editor");
        // 12. As Admin: Click the "Save" button.
        await adminTeam.ClickSave();
        // 13. As Admin: Click the "Tasks" link in the menu.
        await adminMenu.ClickTasks();

        // As Admin: the Tasks page lists "Review security configuration" and does not list "Review the firewall rules".
        await Expect(adminTasks.RowFor("Review security configuration")).ToBeVisibleAsync();
        await adminTasks.ExpectNotListed("Review the firewall rules");
    }
}
