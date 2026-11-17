using Microsoft.Playwright;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Help;

/// <summary>
/// The Help button of the header, and the side panel it opens with the tabs "This page" and "Ask". Every
/// method is a step of a test case of the help area.
///
/// Reading the panel: <see cref="PageLinks"/> is the list of "This page"; in the "Ask" tab
/// <see cref="SectionCards"/> are the cards of the manual sections, <see cref="AnswerLabel"/> marks the written
/// answer and <see cref="BestMatchesHeader"/> the retrieval-only header; <c>PanelMessage(text)</c> is a message
/// shown inside the panel.
/// </summary>
public sealed class HelpPanelPage : BasePage
{
    public HelpPanelPage(IPage page) : base(page) { }

    // ----- Actions -----

    /// <summary>"Click the "Help" button" of the header. Returns when the panel is open and its content has loaded.</summary>
    public async Task ClickHelp()
    {
        LogStep("Click", "Help");
        await HelpButton.ClickAsync();
        await Assertions.Expect(Panel).ToBeVisibleAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Click the "Help" button of the "New Task" form."</summary>
    public async Task ClickHelpOfNewTaskForm()
    {
        LogStep("Click", "Help of the New Task form");
        await Page.GetByTestId("btn-help-task-form").ClickAsync();
        await Assertions.Expect(Panel).ToBeVisibleAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Click the "Ask" tab."</summary>
    public Task ClickAskTab() => AskTab.ClickAsync();

    /// <summary>"Click the "{title}" link" of the list "This page": opens that page in the manual.</summary>
    public async Task ClickPage(string title)
    {
        await PageLink(title).ClickAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Type ... in the "Your question" field."</summary>
    public Task TypeQuestion(string text) => TypeAsync(QuestionField, text);

    /// <summary>"Click the "Ask" button." Returns when the question has been answered and recorded.</summary>
    public async Task ClickAsk()
    {
        LogStep("Click", "Ask");
        await AskButton.ClickAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Click the "Yes" button." Returns when the vote has been sent.</summary>
    public async Task ClickYes()
    {
        await AskPanel.GetByRole(AriaRole.Button, new() { Name = "Yes", Exact = true }).ClickAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Click the "No" button." Returns when the vote has been sent.</summary>
    public async Task ClickNo()
    {
        await AskPanel.GetByRole(AriaRole.Button, new() { Name = "No", Exact = true }).ClickAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Click the source link" of an answer: opens the cited section in the manual.</summary>
    public async Task ClickSource()
    {
        LogStep("Click", "the source link");
        await SourceLinks.ClickAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Click the "Search the manual for this" link" of the fixed reply.</summary>
    public async Task ClickSearchTheManualForThis()
    {
        await AskPanel.GetByRole(AriaRole.Link, new() { Name = "Search the manual for this", Exact = true }).ClickAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>"Click the "Not now" button" of the question about downloading the assistant.</summary>
    public async Task ClickNotNow()
    {
        LogStep("Click", "Not now");
        await AskButtonNamed("Not now").ClickAsync();
    }

    /// <summary>"Press the "Escape" key."</summary>
    public Task PressEscape() => Page.Keyboard.PressAsync("Escape");

    // ----- State: the button and the panel -----

    /// <summary>The "Help" button of the header; a visitor never gets one.</summary>
    public ILocator HelpButton =>
        new HeaderPage(Page).Bar.GetByRole(AriaRole.Button, new() { Name = "Help", Exact = true });

    /// <summary>The side panel; it exists only while it is open.</summary>
    public ILocator Panel => Page.GetByRole(AriaRole.Dialog, new() { Name = "Help", Exact = true });

    /// <summary>The tab "This page".</summary>
    public ILocator ThisPageTab => Panel.GetByRole(AriaRole.Tab, new() { Name = "This page", Exact = true });

    /// <summary>The tab "Ask".</summary>
    public ILocator AskTab => Panel.GetByRole(AriaRole.Tab, new() { Name = "Ask", Exact = true });

    /// <summary>A message shown in the panel, for example "Help is unavailable.".</summary>
    public ILocator PanelMessage(string text) =>
        Panel.GetByRole(AriaRole.Alert).Filter(new() { HasText = text }).First;

    /// <summary>The "Open the manual" link the panel offers when the help cannot be loaded.</summary>
    public ILocator OpenTheManualLink => Panel.GetByRole(AriaRole.Link, new() { Name = "Open the manual", Exact = true });

    // ----- State: the tab "This page" -----

    private ILocator ThisPagePanel => Panel.GetByRole(AriaRole.Tabpanel, new() { Name = "This page", Exact = true });

    /// <summary>The manual pages listed for the screen, in order.</summary>
    public ILocator PageLinks => ThisPagePanel.GetByRole(AriaRole.Link);

    /// <summary>One manual page of the list.</summary>
    public ILocator PageLink(string title) =>
        ThisPagePanel.GetByRole(AriaRole.Link, new() { Name = title, Exact = true });

    // ----- State: the tab "Ask" -----

    private ILocator AskPanel => Panel.GetByRole(AriaRole.Tabpanel, new() { Name = "Ask", Exact = true });

    /// <summary>The "Your question" field.</summary>
    public ILocator QuestionField => AskPanel.GetByLabel("Your question", new() { Exact = true });

    /// <summary>The "Ask" button of the question form.</summary>
    public ILocator AskButton => AskPanel.GetByRole(AriaRole.Button, new() { Name = "Ask", Exact = true });

    /// <summary>The header "Best matches in the manual" above the cards of a retrieval-only result.</summary>
    public ILocator BestMatchesHeader =>
        AskPanel.GetByRole(AriaRole.Heading, new() { Name = "Best matches in the manual", Exact = true });

    /// <summary>The label of the written answer: "Answer from the manual".</summary>
    public ILocator AnswerLabel => AskPanel.GetByText("Answer from the manual", new() { Exact = true });

    /// <summary>The line under the answer, for example "Answered by the server".</summary>
    public ILocator AnswerFooter(string text) => AskPanel.GetByText(text, new() { Exact = true });

    /// <summary>
    /// The source links of an answer, each named with the position of its section, for example "[3]". The
    /// position follows the ranking, so a test names the section the link opens, not its number.
    /// </summary>
    public ILocator SourceLinks =>
        AskPanel.GetByRole(AriaRole.Link, new() { NameRegex = new System.Text.RegularExpressions.Regex(@"^\[\d+\]$") });

    /// <summary>A text of the tab: a fixed reply, a message about the answer, "Did this help?", a thank-you.</summary>
    public ILocator Text(string text) => AskPanel.GetByText(text, new() { Exact = true });

    /// <summary>A link of the tab, for example "Go to the manual home".</summary>
    public ILocator AskLink(string name) =>
        AskPanel.GetByRole(AriaRole.Link, new() { Name = name, Exact = true });

    /// <summary>A button of the tab by its name, for example "Yes", "No".</summary>
    public ILocator AskButtonNamed(string name) =>
        AskPanel.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    /// <summary>The cards of manual sections; a card has no role or name, so it is found by its test id.</summary>
    public ILocator SectionCards => AskPanel.GetByTestId("help-card");

    /// <summary>The first card of the page with this title.</summary>
    public ILocator CardOfPage(string title) =>
        SectionCards.Filter(new() { Has = Page.GetByTestId("help-card-page").Filter(new() { HasText = title }) }).First;

    /// <summary>The page title of the first card.</summary>
    public ILocator FirstCardPageTitle => SectionCards.First.GetByTestId("help-card-page");

    /// <summary>The section heading of the first card.</summary>
    public ILocator FirstCardHeading => SectionCards.First.GetByTestId("help-card-heading");

    /// <summary>The short text of the first card.</summary>
    public ILocator FirstCardText => SectionCards.First.GetByTestId("help-card-text");

    /// <summary>The cards that have no "Open in the manual" link; none is expected.</summary>
    public ILocator CardsWithoutLink =>
        SectionCards.Filter(new()
        {
            HasNot = Page.GetByRole(AriaRole.Link, new() { Name = "Open in the manual", Exact = true })
        });

    /// <summary>The cards shown above the written answer; none is expected.</summary>
    public ILocator CardsAboveTheAnswer => AskPanel.Locator("[data-testid='help-card']:above([data-testid='help-answer'])");

    /// <summary>The cards shown above the message of a declined answer; none is expected.</summary>
    public ILocator CardsAboveTheDeclinedMessage => AskPanel.Locator("[data-testid='help-card']:above([data-testid='help-declined'])");

    /// <summary>The number of cards, once the first one is shown.</summary>
    public async Task<int> CountCards()
    {
        await Assertions.Expect(SectionCards.First).ToBeVisibleAsync();
        return await SectionCards.CountAsync();
    }
}
