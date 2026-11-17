using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.PageObjects.Tasks;

namespace MyApp.E2E.Tests.Tasks;

/// <summary>
/// TASK-EDIT-005: Cancelling an edit keeps the previous values
/// </summary>
[TestFixture]
[Category("tasks")]
[Property("TestCase", "TASK-EDIT-005")]
[Property("UserStory", "US-TASK-02")]
public class TaskEdit005Tests : TasksTestBase
{
    protected override TestUser GetTestUser() => TestUsers.MemberUser;

    [Test]
    [Description("Clicking 'Cancel' closes the edit form and the task keeps its title.")]
    public async Task CancellingAnEditKeepsThePreviousValues()
    {
        var tasks = new TasksPage(Page);

        // 1. Open the Tasks page.
        await tasks.Open();
        // 2. Click the "Edit" button on the row of "Set up monitoring dashboard".
        await tasks.ClickEdit("Set up monitoring dashboard");
        // 3. Clear the "Title" field.
        await tasks.ClearTitle();
        // 4. Type "Changed title" in the "Title" field.
        await tasks.TypeTitle("Changed title");
        // 5. Click the "Cancel" button.
        await tasks.ClickCancel();

        // The form is closed.
        await Expect(tasks.TitleField).ToBeHiddenAsync();
        // The list shows "Set up monitoring dashboard" and does not show "Changed title".
        await Expect(tasks.RowFor("Set up monitoring dashboard")).ToBeVisibleAsync();
        await tasks.ExpectNotListed("Changed title");
    }
}
