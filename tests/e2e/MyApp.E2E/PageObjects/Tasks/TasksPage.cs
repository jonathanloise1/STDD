using Microsoft.Playwright;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Tasks;

/// <summary>
/// The Tasks screen (`/tasks`): the table of tasks, the New Task / edit form above it and the "Delete Task"
/// dialog. Every method is a step of a test case of the tasks area.
///
/// Reading the screen: <see cref="RowFor"/> is the row of a task by its title; <see cref="StatusOf"/>,
/// <see cref="DueDateOf"/> and <see cref="TitleCellOf"/> are cells of that row; <see cref="FieldMessage"/> is
/// the message under a form field; <c>Message(text)</c> is a message of the application (toast or alert).
/// </summary>
public sealed class TasksPage : BasePage
{
    protected override string? PagePath => "/tasks";

    public TasksPage(IPage page) : base(page) { }

    /// <summary>"Open the Tasks page." Returns when the list, the empty message or an error is shown.</summary>
    public override async Task Open()
    {
        await base.Open();
        await WaitForList();
    }

    /// <summary>
    /// Waits until the page has finished loading the tasks: the table, the empty message or an error
    /// is shown instead of the spinner. Needed before saying that something is not listed.
    /// </summary>
    public Task WaitForList() =>
        Assertions.Expect(Table.Or(EmptyMessage).Or(Page.GetByRole(AriaRole.Alert)).First).ToBeVisibleAsync();

    /// <summary>
    /// "The list does not show ...": waits for the list to be loaded, then checks that no row has this
    /// title or description.
    /// </summary>
    public async Task ExpectNotListed(string text)
    {
        await WaitForList();
        await Assertions.Expect(RowFor(text)).ToHaveCountAsync(0);
    }

    // ----- Actions: the form -----

    /// <summary>"Click the "New Task" button."</summary>
    public Task ClickNewTask() => Button("New Task").ClickAsync();

    /// <summary>"Type ... in the "Title" field." Replaces what the field holds.</summary>
    public Task TypeTitle(string text) => TypeAsync(Field("Title"), text);

    /// <summary>"Type ... in the "Due Date" field." The format is yyyy-MM-dd.</summary>
    public Task TypeDueDate(string text) => TypeAsync(Field("Due Date"), text);

    /// <summary>"Type ... in the "Description" field."</summary>
    public Task TypeDescription(string text) => TypeAsync(Field("Description"), text);

    /// <summary>"Clear the "Title" field."</summary>
    public Task ClearTitle() => Field("Title").ClearAsync();

    /// <summary>"Select ... in the "Status" field." The label is as shown: "Todo", "In Progress", "Done", "Cancelled".</summary>
    public Task SelectStatus(string label) =>
        Field("Status").SelectOptionAsync(new SelectOptionValue { Label = label });

    /// <summary>"Click the "Create Task" button." Returns when saving has finished, whatever the outcome.</summary>
    public async Task ClickCreateTask()
    {
        await Button("Create Task").ClickAsync();
        await WaitUntilSavedAsync();
    }

    /// <summary>"Click the "Update" button." Returns when saving has finished, whatever the outcome.</summary>
    public async Task ClickUpdate()
    {
        await Button("Update").ClickAsync();
        await WaitUntilSavedAsync();
    }

    /// <summary>"Click the "Cancel" button" of the form.</summary>
    public Task ClickCancel() => Button("Cancel").ClickAsync();

    // ----- Actions: the rows -----

    /// <summary>"Click the "Edit" button on the row of ..."</summary>
    public Task ClickEdit(string title) => RowFor(title).GetByRole(AriaRole.Button, NamedIn("Edit")).ClickAsync();

    /// <summary>
    /// "Click the "Change status" button on the row of ..." Returns when the request has finished, whatever
    /// the outcome.
    /// </summary>
    public async Task ClickChangeStatus(string title)
    {
        await RowFor(title).GetByRole(AriaRole.Button, new() { Name = "Change status", Exact = true }).ClickAsync();
        await WaitUntilSavedAsync();
    }

    /// <summary>"Click the "Delete" button on the row of ..." Opens the "Delete Task" dialog.</summary>
    public Task ClickDelete(string title) => RowFor(title).GetByRole(AriaRole.Button, NamedIn("Delete")).ClickAsync();

    // ----- Actions: the "Delete Task" dialog -----

    /// <summary>
    /// "Click the "Delete" button in the dialog." Returns when the dialog has closed, which it does once the
    /// request has finished, whatever the outcome.
    /// </summary>
    public async Task ClickDeleteInDialog()
    {
        await DeleteDialog.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
        await Assertions.Expect(DeleteDialog).ToBeHiddenAsync();
    }

    /// <summary>"Click the "Cancel" button in the dialog."</summary>
    public Task ClickCancelInDialog() => DeleteDialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

    /// <summary>
    /// Deletes the task if it is listed, through the same buttons a user clicks. For clean-up after a test
    /// that created it; does nothing when the row or the button is not there.
    /// </summary>
    public async Task DeleteIfListed(string title)
    {
        var deleteButton = RowFor(title).GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true });
        if (await deleteButton.CountAsync() == 0)
            return;

        await deleteButton.ClickAsync();
        await ClickDeleteInDialog();
    }

    // ----- State: the list -----

    /// <summary>The table of tasks.</summary>
    public ILocator Table => Page.GetByRole(AriaRole.Table);

    /// <summary>The column headers of the table, in order.</summary>
    public ILocator ColumnHeaders => Table.GetByRole(AriaRole.Columnheader);

    /// <summary>One column header, for example "Actions".</summary>
    public ILocator ColumnHeader(string name) =>
        Table.GetByRole(AriaRole.Columnheader, new() { Name = name, Exact = true });

    /// <summary>The rows of tasks, newest first (the header row is not included).</summary>
    public ILocator Rows => Table.Locator("tbody").GetByRole(AriaRole.Row);

    /// <summary>The first row of the list.</summary>
    public ILocator FirstRow => Rows.First;

    /// <summary>The row of the task with this title.</summary>
    public ILocator RowFor(string title) =>
        Rows.Filter(new() { Has = Page.GetByText(title, new() { Exact = true }) });

    /// <summary>The first cell of a row: the title with the description below it.</summary>
    public ILocator TitleCellOf(string title) => RowFor(title).GetByRole(AriaRole.Cell).Nth(0);

    /// <summary>The status label of a row ("Todo", "In Progress", "Done", "Cancelled").</summary>
    public ILocator StatusOf(string title) => RowFor(title).GetByRole(AriaRole.Cell).Nth(1);

    /// <summary>The due date of a row, or "-" when it has none.</summary>
    public ILocator DueDateOf(string title) => RowFor(title).GetByRole(AriaRole.Cell).Nth(2);

    /// <summary>Every "Edit" button on the page; none for a Viewer.</summary>
    public ILocator EditButtons => Page.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true });

    /// <summary>Every "Change status" button on the page; none for a Viewer.</summary>
    public ILocator ChangeStatusButtons => Page.GetByRole(AriaRole.Button, new() { Name = "Change status", Exact = true });

    /// <summary>Every "Delete" button on the page; none for a Viewer.</summary>
    public ILocator DeleteButtons => Page.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true });

    /// <summary>The "New Task" button; Viewers do not see it.</summary>
    public ILocator NewTaskButton => Button("New Task");

    /// <summary>"No tasks yet".</summary>
    public ILocator EmptyMessage => ExactText("No tasks yet");

    /// <summary>"Create your first task to get started."</summary>
    public ILocator EmptyHint => ExactText("Create your first task to get started.");

    // ----- State: the form -----

    /// <summary>The "Title" field. It is visible exactly while the form is open.</summary>
    public ILocator TitleField => Field("Title");

    /// <summary>The "Status" field, which only the edit form has.</summary>
    public ILocator StatusField => Field("Status");

    /// <summary>
    /// A field of the form by its label ("Title", "Due Date", "Description", "Status"), for assertions on
    /// its value.
    /// </summary>
    public ILocator FormField(string label) => Field(label);

    /// <summary>
    /// The message under a field of the form, for example "Enter a title." under "Title". It is the element
    /// that follows the field; it does not exist while the field has no message.
    /// </summary>
    public ILocator FieldMessage(string label) => Field(label).Locator("xpath=following-sibling::*[1]");

    // ----- State: the dialog -----

    /// <summary>The "Delete Task" dialog.</summary>
    public ILocator DeleteDialog => Page.GetByRole(AriaRole.Dialog, new() { Name = "Delete Task" });

    // ----- Waiting -----

    /// <summary>
    /// Saving, changing a status and deleting disable their button until the request has finished. This
    /// waits until no such button is disabled, which is also the case when the form has closed and when the
    /// row is gone. It gives a following step, or a second session, a settled state to start from.
    /// </summary>
    private async Task WaitUntilSavedAsync()
    {
        await Page.WaitForFunctionAsync(
            "() => !document.querySelector('form button[type=submit]:disabled, tbody button:disabled')");
    }
}
