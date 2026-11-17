using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Dashboard;

/// <summary>
/// The Dashboard screen (`/dashboard`): the greeting, the four count cards and the "Recent Tasks" section.
/// Every method is a step of a test case of the dashboard area.
///
/// Reading the screen: <see cref="Card"/> is a count card by its label and <see cref="CardValue"/> its number;
/// <see cref="RecentTasks"/> is the section below, with <see cref="RecentRowFor"/> and its cells.
/// </summary>
public sealed class DashboardPage : BasePage
{
    protected override string? PagePath => "/dashboard";

    public DashboardPage(IPage page) : base(page) { }

    // ----- Actions -----

    /// <summary>"Click the "View All" button."</summary>
    public async Task ClickViewAll()
    {
        await Button("View All").ClickAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Click the row of ... in the "Recent Tasks" table."</summary>
    public async Task ClickRecentTask(string title)
    {
        await RecentRowFor(title).ClickAsync();
        await WaitForPageLoadAsync();
    }

    // ----- State: greeting -----

    /// <summary>The greeting with the user name, for example "Good morning, Dev User".</summary>
    public ILocator Greeting => Page.GetByRole(AriaRole.Heading,
        new() { NameRegex = new Regex("^(Good morning|Good afternoon|Good evening|Buongiorno|Buon pomeriggio|Buonasera)") });

    /// <summary>The line under the greeting in English: "Here is your task overview".</summary>
    public ILocator OverviewLine => Page.GetByText("Here is your task overview", new() { Exact = true });

    /// <summary>The line under the greeting in Italian.</summary>
    public ILocator OverviewLineInItalian => Page.GetByText("Ecco la panoramica delle tue attività", new() { Exact = true });

    // ----- State: the cards -----

    /// <summary>A count card by its label: "Total Tasks", "Todo", "In Progress" or "Done".</summary>
    public ILocator Card(string label) =>
        Page.GetByRole(AriaRole.Group, new() { Name = label, Exact = true });

    /// <summary>The number of a count card.</summary>
    public ILocator CardValue(string label) => Card(label).Locator("xpath=./div[1]");

    /// <summary>The number of a count card, once it is shown.</summary>
    public async Task<int> CountOf(string label)
    {
        await Assertions.Expect(CardValue(label)).ToHaveTextAsync(new Regex(@"^\d+$"));
        return int.Parse(await CardValue(label).InnerTextAsync());
    }

    // ----- State: recent tasks -----

    /// <summary>The "Recent Tasks" section: its title, the "View All" button and the table or message.</summary>
    public ILocator RecentTasks =>
        Page.Locator(".card").Filter(new() { Has = ExactText("Recent Tasks") });

    /// <summary>The table of recent tasks.</summary>
    public ILocator RecentTable => RecentTasks.GetByRole(AriaRole.Table);

    /// <summary>The column headers of the table, in order.</summary>
    public ILocator RecentColumnHeaders => RecentTable.GetByRole(AriaRole.Columnheader);

    /// <summary>The rows of the table, newest first (the header row is not included).</summary>
    public ILocator RecentRows => RecentTable.Locator("tbody").GetByRole(AriaRole.Row);

    /// <summary>The row of the task with this title.</summary>
    public ILocator RecentRowFor(string title) =>
        RecentRows.Filter(new() { Has = Page.GetByText(title, new() { Exact = true }) });

    /// <summary>The status label of a row.</summary>
    public ILocator RecentStatusOf(string title) => RecentRowFor(title).GetByRole(AriaRole.Cell).Nth(1);

    /// <summary>The due date of a row, or "-" when it has none.</summary>
    public ILocator RecentDueDateOf(string title) => RecentRowFor(title).GetByRole(AriaRole.Cell).Nth(2);

    /// <summary>"No tasks yet. Go to Tasks to create your first one." in the section.</summary>
    public ILocator RecentHint =>
        RecentTasks.GetByText("No tasks yet. Go to Tasks to create your first one.", new() { Exact = true });

    /// <summary>Any "No tasks yet" text in the section.</summary>
    public ILocator RecentNoTasksText => RecentTasks.GetByText("No tasks yet");

    /// <summary>A message of the application inside the "Recent Tasks" section.</summary>
    public ILocator RecentMessage(string text) =>
        RecentTasks.GetByRole(AriaRole.Alert).Filter(new() { HasText = text }).First;
}
