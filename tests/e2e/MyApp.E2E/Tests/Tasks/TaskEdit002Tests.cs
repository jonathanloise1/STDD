using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-EDIT-002: Updating with an empty title shows a field message
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-EDIT-002")]
[Property("UserStory", "US-TASK-02")]
public class TaskEdit002Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("A task cannot be updated while the Title field is empty; the field says why and the task keeps its title.")]
    public async Task UpdatingWithAnEmptyTitleShowsAFieldMessage()
    {
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Edit" button on the row of "Add audit logging".
        await tasks.ClickEdit("Add audit logging");
        // 3. Clear the "Title" field.
        await tasks.ClearTitle();
        // 4. Click the "Update" button.
        await tasks.ClickUpdate();

        // After step 4, the form stays open and the "Title" field shows "Enter a title."
        await Expect(tasks.TitleField).ToBeVisibleAsync();
        await Expect(tasks.FieldMessage("Title")).ToHaveTextAsync("Enter a title.");

        // 5. Click the "Cancel" button.
        await tasks.ClickCancel();

        // After step 5, the list still shows "Add audit logging".
        await Expect(tasks.RowFor("Add audit logging")).ToBeVisibleAsync();
    }
}
