using Microsoft.Playwright;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Help;

/// <summary>
/// The Manual screen (`/manual`): the home page with its areas, an area page, a page of an area, and the
/// search results (`/manual/search`) with the "Search the manual" field above them. Every method is a step of a
/// test case of the help area.
///
/// Reading the screen: <see cref="PageTitle"/> and <see cref="SectionHeading"/> are the headings of the page
/// that is open; <see cref="ListedLinks"/> are the areas of the home page or the pages of an area;
/// <see cref="Results"/> are the search results, each one link holding page title, heading and snippet.
/// </summary>
public sealed class ManualPage : BasePage
{
    protected override string? PagePath => "/manual";

    public ManualPage(IPage page) : base(page) { }

    // ----- Actions -----

    /// <summary>
    /// "Open the Manual page". The page is ready when it shows what the user reads: a page of the manual, the
    /// message that the manual could not be loaded, or the message that the page does not exist. It does not wait
    /// for the network to be idle, because a manual that cannot be loaded is a state of this page, not a
    /// loading page (the manual files are static; the application does not retry them).
    /// </summary>
    public override async Task Open()
    {
        LogStep("Open", PagePath!);
        await Page.GotoAsync($"{TestConfiguration.BaseUrl}{PagePath}");
        // A visitor is sent to sign in instead: that is the other state the page can end in.
        var shown = Article.Or(LoadFailedMessage).Or(Page.GetByTestId("manual-page-missing")).First.WaitForAsync();
        var signIn = Page.WaitForURLAsync(url =>
            new Uri(url).Host.Equals(IdentityProviderStandIn.Host, StringComparison.OrdinalIgnoreCase));
        var first = await Task.WhenAny(shown, signIn);
        await first;
    }

    /// <summary>
    /// "Open the Manual page" from the menu, without loading the application again, so the language of the
    /// session is kept. The entry is found by its menu id because its name follows the language.
    /// </summary>
    public async Task OpenFromMenu()
    {
        LogStep("Open", "Manual from the menu");
        await new SidebarPage(Page).EntryById(SidebarPage.ManualId).ClickAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Click the "{name}" link" of the page: an area of the home page, a page of an area.</summary>
    public async Task ClickLink(string name)
    {
        LogStep("Click link", name);
        await Article.GetByRole(AriaRole.Link, new() { Name = name, Exact = true }).ClickAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Type ... in the "Search the manual" field."</summary>
    public Task TypeSearch(string text) => TypeAsync(SearchField, text);

    /// <summary>"Click the "Search" button." Returns when the results page has loaded.</summary>
    public async Task ClickSearch()
    {
        LogStep("Click", "Search");
        await Button("Search").ClickAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Click the result of the page "{title}"": the first result with that page title.</summary>
    public async Task ClickResult(string title)
    {
        await ResultOfPage(title).ClickAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Click the "Go to the manual home" link."</summary>
    public async Task ClickGoToTheManualHome()
    {
        await Link("Go to the manual home").ClickAsync();
        await WaitForPageLoadAsync();
    }

    // ----- State: browsing -----

    /// <summary>The page that is open.</summary>
    public ILocator Article => Page.GetByRole(AriaRole.Article);

    /// <summary>The title of the open page, the only first-level heading.</summary>
    public ILocator PageTitle(string title) =>
        Article.GetByRole(AriaRole.Heading, new() { Name = title, Exact = true, Level = 1 });

    /// <summary>The introduction of the open page: its first paragraph.</summary>
    public ILocator Introduction => Article.Locator("p").First;

    /// <summary>A section heading of the open page, for example "Steps" or "What you will see".</summary>
    public ILocator SectionHeading(string name) =>
        Article.GetByRole(AriaRole.Heading, new() { Name = name, Exact = true, Level = 2 });

    /// <summary>The links of the list at the end of a home page (its areas) or an area page (its pages), in order.</summary>
    public ILocator ListedLinks =>
        Article.Locator("xpath=./h2[last()]/following-sibling::ul[1]").GetByRole(AriaRole.Link);

    /// <summary>One link of that list.</summary>
    public ILocator ListedLink(string name) =>
        Article.Locator("xpath=./h2[last()]/following-sibling::ul[1]")
            .GetByRole(AriaRole.Link, new() { Name = name, Exact = true });

    /// <summary>The message "The manual could not be loaded." (an alert of the page).</summary>
    public ILocator LoadFailedMessage => Message("The manual could not be loaded.");

    // ----- State: search -----

    /// <summary>The "Search the manual" field.</summary>
    public ILocator SearchField => Field("Search the manual");

    /// <summary>The search results, one link each; it has no accessible name, so it is found by its test id.</summary>
    public ILocator Results => Page.GetByTestId("search-results").GetByRole(AriaRole.Link);

    /// <summary>The first result whose page title is this one.</summary>
    public ILocator ResultOfPage(string title) =>
        Results.Filter(new() { Has = Page.GetByText(title, new() { Exact = true }) }).First;

    /// <summary>The page title of a result, its first line.</summary>
    public ILocator ResultTitle(ILocator result) => result.Locator("xpath=./span[1]");

    /// <summary>The section heading of a result, its second line.</summary>
    public ILocator ResultHeading(ILocator result) => result.Locator("xpath=./span[2]");

    /// <summary>The short text of a result, its third line.</summary>
    public ILocator ResultSnippet(ILocator result) => result.Locator("xpath=./span[3]");

    /// <summary>The words shown in bold in the results.</summary>
    public ILocator BoldWords => Page.GetByTestId("search-results").Locator("strong");

    /// <summary>The length of the short text of every result, once results are shown.</summary>
    public async Task<IReadOnlyList<int>> SnippetLengths()
    {
        await Assertions.Expect(Results.First).ToBeVisibleAsync();
        var texts = await Results.Locator("xpath=./span[3]").AllInnerTextsAsync();
        return texts.Select(text => text.Trim().Length).ToList();
    }

    /// <summary>"Nothing found in the manual."</summary>
    public ILocator NothingFoundMessage => Page.GetByText("Nothing found in the manual.", new() { Exact = true });

    /// <summary>The "Go to the manual home" link shown when nothing matches.</summary>
    public ILocator GoToTheManualHomeLink => Link("Go to the manual home");

    /// <summary>"Help is unavailable." where the results would be.</summary>
    public ILocator UnavailableMessage => Message("Help is unavailable.");

    /// <summary>What a finished search shows: its results, or the message that nothing matches.</summary>
    public ILocator SearchOutcome => Results.First.Or(NothingFoundMessage);
}
