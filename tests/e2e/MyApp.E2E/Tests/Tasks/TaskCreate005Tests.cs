using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Common;
using MyApp.E2E.PageObjects.Organizations;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-CREATE-005: Saving after the role became Viewer shows a message
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-CREATE-005")]
[Property("UserStory", "US-TASK-01")]
public class TaskCreate005Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("An editor whose role is changed to Viewer while the New Task form is open cannot save the task and is told why.")]
    public async Task SavingAfterTheRoleBecameViewerShowsAMessage()
    {
        var title = UniqueTitle("Prepare the quarterly report");
        var tasks = new TasksPage(Page);
        // A second session is signed in as Admin of "MyApp Dev Org".
        var admin = await StartSecondSession(TestUsers.AdminUser);
        var adminMenu = new SidebarPage(admin);
        var adminTeam = new TeamPage(admin);
        var adminTasks = new TasksPage(admin);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "New Task" button.
        await tasks.ClickNewTask();
        // 3. Type the title in the "Title" field.
        await tasks.TypeTitle(title);
        // 4. As Admin: Click the "Team" link in the menu.
        await adminMenu.ClickTeam();
        // 5. As Admin: Click the "Edit" button on the row of "Dev Member".
        await adminTeam.ClickEdit(TestUsers.MemberUser.Name);
        // 6. As Admin: Check the "Viewer" field.
        await adminTeam.CheckRole("Viewer");
        // 7. As Admin: Click the "Save" button.
        await adminTeam.ClickSave();
        // 8. Click the "Create Task" button.
        await tasks.ClickCreateTask();
        // After step 8, the message "Your role does not allow this change." appears.
        await Expect(tasks.Message("Your role does not allow this change.")).ToBeVisibleAsync();
        // 9. As Admin: Click the "Edit" button on the row of "Dev Member".
        await adminTeam.ClickEdit(TestUsers.MemberUser.Name);
        // 10. As Admin: Check the "Editor" field.
        await adminTeam.CheckRole("Editor");
        // 11. As Admin: Click the "Save" button.
        await adminTeam.ClickSave();
        // 12. As Admin: Click the "Tasks" link in the menu.
        await adminMenu.ClickTasks();

        // As Admin: the Tasks page does not list the title.
        await adminTasks.ExpectNotListed(title);
    }
}
