using Microsoft.Playwright;

namespace MyApp.E2E.PageObjects.Common;

/// <summary>
/// The menu on the left of every screen (the sidebar). Its entries are links named by their label, and the
/// entry of the open screen carries <c>aria-current="page"</c>. What the menu offers depends on the role:
/// Admins also see "Team", "Audit Log" and "Settings".
/// </summary>
public sealed class SidebarPage : BasePage
{
    public const string Dashboard = "Dashboard";
    public const string Tasks = "Tasks";
    public const string Team = "Team";
    public const string AuditLog = "Audit Log";
    public const string Settings = "Settings";
    public const string ManualId = "manual";
    public const string UnansweredQuestionsId = "unanswered";

    public SidebarPage(IPage page) : base(page) { }

    // ----- Actions: "Click the "Tasks" link in the menu." -----

    public Task ClickDashboard() => ClickEntry(Dashboard);

    public Task ClickTasks() => ClickEntry(Tasks);

    public Task ClickTeam() => ClickEntry(Team);

    public Task ClickAuditLog() => ClickEntry(AuditLog);

    public Task ClickSettings() => ClickEntry(Settings);

    /// <summary>Clicks the menu entry with this label and waits for the next screen.</summary>
    public async Task ClickEntry(string label)
    {
        LogStep("Click menu entry", label);
        await Entry(label).ClickAsync();
        await WaitForPageLoadAsync();
    }

    // ----- State -----

    /// <summary>The menu itself.</summary>
    public ILocator Menu => Page.GetByRole(AriaRole.Navigation).First;

    /// <summary>The menu entry with this label.</summary>
    public ILocator Entry(string label) =>
        Menu.GetByRole(AriaRole.Link, new() { Name = label, Exact = true });

    /// <summary>
    /// The menu entry of the screen with this id (<c>manual</c>, <c>unanswered</c>, ...). The name of an entry
    /// follows the language of the session, so a step that must work in any language finds it by the id the menu
    /// gives every entry (<c>sidebar-{id}</c> in the menu definition).
    /// </summary>
    public ILocator EntryById(string id) => Menu.Locator($"#sidebar-{id}");

    /// <summary>The menu entry of the screen that is open.</summary>
    public ILocator ActiveEntry => Menu.GetByRole(AriaRole.Link).And(Page.Locator("[aria-current='page']"));

    /// <summary>All the entries of the menu.</summary>
    public ILocator Entries => Menu.GetByRole(AriaRole.Link);

    /// <summary>The labels of the entries, in the order they are shown.</summary>
    public async Task<IReadOnlyList<string>> EntryLabels() =>
        await Entries.EvaluateAllAsync<string[]>("links => links.map(l => l.getAttribute('aria-label') ?? l.textContent.trim())");
}
