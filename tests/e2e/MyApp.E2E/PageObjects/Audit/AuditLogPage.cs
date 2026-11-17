using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Audit;

/// <summary>
/// The Audit Log screen (`/audit`, Admins only): the filters, the table of changes with its count and paging,
/// and the "Details" dialog. Every method is a step of a test case of the audit area.
///
/// Reading the screen: <see cref="Rows"/> are the changes of the page; <see cref="Total"/> is the count above the
/// table; <see cref="DetailsDialog"/> holds <see cref="OldValues"/> and <see cref="NewValues"/>, whose lines are
/// read with <see cref="ValueIn"/>.
/// </summary>
public sealed class AuditLogPage : BasePage
{
    protected override string? PagePath => "/audit";

    public AuditLogPage(IPage page) : base(page) { }

    // ----- Actions: filters -----

    /// <summary>"Select ... in the "Type" field." The label is as shown: "All types", "Task", "Team member".</summary>
    public async Task SelectType(string label)
    {
        await TypeField.SelectOptionAsync(new SelectOptionValue { Label = label });
        await WaitForPageLoadAsync();
    }

    /// <summary>
    /// "Select the second choice of the "Type" field": the choices are listed in the same order in every
    /// language, "All types" being the first, so this is "Task".
    /// </summary>
    public async Task SelectTypeNumber(int position)
    {
        await TypeField.SelectOptionAsync(new SelectOptionValue { Index = position });
        await WaitForPageLoadAsync();
    }

    /// <summary>"Type ... in the "From" field." The format is yyyy-MM-dd.</summary>
    public async Task TypeFrom(string date)
    {
        await FromField.FillAsync(date);
        await WaitForPageLoadAsync();
    }

    /// <summary>"Click the "Reset" button."</summary>
    public async Task ClickReset()
    {
        await Button("Reset").ClickAsync();
        await WaitForPageLoadAsync();
    }

    // ----- Actions: paging -----

    /// <summary>"Click the "Next" button."</summary>
    public async Task ClickNext()
    {
        await Button("Next").ClickAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Click the "Previous" button."</summary>
    public async Task ClickPrevious()
    {
        await Button("Previous").ClickAsync();
        await WaitForPageLoadAsync();
    }

    // ----- Actions: details -----

    /// <summary>"Click the "Details" button on the first row of the table." Opens the "Details" dialog.</summary>
    public Task ClickDetailsOfFirstRow() =>
        FirstRow.GetByRole(AriaRole.Button, NamedIn("Details")).ClickAsync();

    /// <summary>"Click the "Cancel" button" of the "Details" dialog.</summary>
    public Task ClickCancelInDialog() =>
        DetailsDialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

    // ----- State: the screen -----

    /// <summary>The page title.</summary>
    public ILocator Heading => HeadingOf("Audit Log");

    // ----- State: filters -----

    /// <summary>The "Type" field.</summary>
    public ILocator TypeField => Field("Type");

    /// <summary>The choice the "Type" field shows, for example "All types".</summary>
    public ILocator TypeChoice => TypeField.Locator("option:checked");

    /// <summary>Every choice of the "Type" field, in order.</summary>
    public ILocator TypeChoices => TypeField.GetByRole(AriaRole.Option);

    /// <summary>The "From" field.</summary>
    public ILocator FromField => Field("From");

    /// <summary>The "To" field.</summary>
    public ILocator ToField => Field("To");

    // ----- State: the table -----

    /// <summary>The count above the table, for example "31 changes".</summary>
    public ILocator Total => Page.GetByTestId("audit-total");

    /// <summary>The count depends on the changes earlier runs made, so the manual capture masks it.</summary>
    public override IReadOnlyList<ILocator> VolatileRegions => new[] { Total };

    /// <summary>The number of changes the count shows.</summary>
    public async Task<int> TotalCount()
    {
        await Assertions.Expect(Total).ToHaveTextAsync(new Regex(@"^\d+ changes?$"));
        return int.Parse(Regex.Match(await Total.InnerTextAsync(), @"\d+").Value);
    }

    /// <summary>The table of changes.</summary>
    public ILocator Table => Page.GetByRole(AriaRole.Table);

    /// <summary>The column headers of the table, in order.</summary>
    public ILocator ColumnHeaders => Table.GetByRole(AriaRole.Columnheader);

    /// <summary>The rows of changes, newest first (the header row is not included).</summary>
    public ILocator Rows => Table.Locator("tbody").GetByRole(AriaRole.Row);

    /// <summary>The first row of the table.</summary>
    public ILocator FirstRow => Rows.First;

    /// <summary>The rows whose "Type" column is not this type.</summary>
    public ILocator RowsNotOfType(string type) =>
        Rows.Filter(new() { HasNot = Page.GetByRole(AriaRole.Cell, new() { Name = type, Exact = true }) });

    /// <summary>The action of a row ("Create", "Update", "Delete").</summary>
    public ILocator ActionOf(ILocator row) => row.GetByRole(AriaRole.Cell).Nth(1);

    /// <summary>The type of a row.</summary>
    public ILocator TypeOf(ILocator row) => row.GetByRole(AriaRole.Cell).Nth(2);

    /// <summary>The user of a row.</summary>
    public ILocator UserOf(ILocator row) => row.GetByRole(AriaRole.Cell).Nth(3);

    /// <summary>The "Changes" column of a row: the changed fields, or "-".</summary>
    public ILocator ChangesOf(ILocator row) => row.GetByRole(AriaRole.Cell).Nth(4);

    /// <summary>The texts of the rows now shown, to compare with the rows shown later.</summary>
    public async Task<IReadOnlyList<string>> RowTexts() => await Rows.AllTextContentsAsync();

    /// <summary>"No audit logs found."</summary>
    public ILocator EmptyMessage => ExactText("No audit logs found.");

    /// <summary>The "Previous" button; it is not shown when there is one page only.</summary>
    public ILocator PreviousButton => Button("Previous");

    /// <summary>The "Next" button; it is not shown when there is one page only.</summary>
    public ILocator NextButton => Button("Next");

    /// <summary>The page position, for example "2 / 3": the text that starts with this page number and " /".</summary>
    public ILocator PagePosition(int page) => Page.GetByText(new Regex($@"^{page} / \d+$"));

    // ----- State: the "Details" dialog -----

    /// <summary>The "Details" dialog.</summary>
    public ILocator DetailsDialog => Page.GetByRole(AriaRole.Dialog, new() { Name = "Details", Exact = true });

    /// <summary>A line of the top of the dialog by its label: "Action", "Type", "User" or "Date/Time".</summary>
    public ILocator DetailOf(string label) => ValueIn(DetailsDialog, label);

    /// <summary>The "Old Values" block of the dialog; it does not exist when the change has no old values.</summary>
    public ILocator OldValues => DetailsDialog.GetByRole(AriaRole.Region, new() { Name = "Old Values", Exact = true });

    /// <summary>The "New Values" block of the dialog.</summary>
    public ILocator NewValues => DetailsDialog.GetByRole(AriaRole.Region, new() { Name = "New Values", Exact = true });

    /// <summary>The value shown beside a label, inside a block or the dialog: <c>ValueIn(OldValues, "Title")</c>.</summary>
    public ILocator ValueIn(ILocator block, string label) =>
        block.Locator($"xpath=.//dt[normalize-space()='{label}']/following-sibling::dd[1]");
}
