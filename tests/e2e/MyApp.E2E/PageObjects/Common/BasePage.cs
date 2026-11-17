using Microsoft.Playwright;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.PageObjects.Common;

/// <summary>
/// Base class of every page object (development.md 10.2).
///
/// A page object is the only place that holds locators. It offers:
/// - actions named as the steps of a test case: <c>ClickNewTask()</c>, <c>TypeTitle("...")</c>;
/// - readable state for assertions, as locators the test hands to <c>Expect(...)</c>:
///   <c>RowFor(title)</c>, <c>FieldMessage("Title")</c>.
///
/// Locators prefer accessible role, label and text. A test id is used only when the element has no
/// accessible name.
/// </summary>
public abstract class BasePage
{
    protected readonly IPage Page;

    /// <summary>The path of the screen, for example "/tasks"; null for a part shared by every screen.</summary>
    protected virtual string? PagePath => null;

    protected BasePage(IPage page)
    {
        Page = page;
    }

    /// <summary>
    /// "Open the {screen} page": goes to the path of the screen and waits for it to settle.
    /// </summary>
    public virtual async Task Open()
    {
        if (PagePath is null)
            throw new InvalidOperationException($"{GetType().Name} is a shared part with no path of its own.");

        LogStep("Open", PagePath);
        await Page.GotoAsync($"{TestConfiguration.BaseUrl}{PagePath}");
        await WaitForPageLoadAsync();
    }

    /// <summary>"Reload the page."</summary>
    public virtual async Task Reload()
    {
        LogStep("Reload");
        await Page.ReloadAsync();
        await WaitForPageLoadAsync();
    }

    /// <summary>Waits until the screen has no request in flight.</summary>
    public virtual async Task WaitForPageLoadAsync()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    // ----- State every screen shares -----

    /// <summary>
    /// A message the application shows after a request: an error toast, a success toast or an inline alert.
    /// Match the exact English text from the test case ("Your role does not allow this change.").
    /// </summary>
    public ILocator Message(string text) =>
        Page.GetByRole(AriaRole.Alert).Filter(new() { HasText = text }).First;

    /// <summary>
    /// The parts of the screen whose content changes from run to run (values with a generated suffix, counts
    /// that depend on earlier runs). The manual capture masks them on every shot of the screen
    /// (manual.md 4.4). Empty for a screen with none.
    /// </summary>
    public virtual IReadOnlyList<ILocator> VolatileRegions => Array.Empty<ILocator>();

    // ----- Helpers for the page objects -----

    protected ILocator Button(string name) =>
        Page.GetByRole(AriaRole.Button, Named(name));

    protected ILocator Link(string name) =>
        Page.GetByRole(AriaRole.Link, Named(name));

    /// <summary>A form field found by its label, as a user finds it.</summary>
    protected ILocator Field(string label) =>
        PageText.IsItalian
            ? Page.GetByLabel(PageText.Exact(label))
            : Page.GetByLabel(label, new() { Exact = true });

    /// <summary>Text shown on the screen, found by its whole content (English, or Italian in an Italian run).</summary>
    protected ILocator ExactText(string english) =>
        PageText.IsItalian
            ? Page.GetByText(PageText.Exact(english))
            : Page.GetByText(english, new() { Exact = true });

    /// <summary>A heading found by its whole text (English, or Italian in an Italian run), at any level or one level.</summary>
    protected ILocator HeadingOf(string english, int? level = null) =>
        Page.GetByRole(AriaRole.Heading, level is { } l
            ? (PageText.IsItalian ? new() { NameRegex = PageText.Exact(english), Level = l } : new() { Name = english, Exact = true, Level = l })
            : Named(english));

    /// <summary>A heading made of a translated text and a fixed tail, such as " - MyApp Dev Org".</summary>
    protected ILocator HeadingWithTail(string english, string tail) =>
        PageText.IsItalian
            ? Page.GetByRole(AriaRole.Heading, new() { NameRegex = PageText.ExactWithTail(english, tail) })
            : Page.GetByRole(AriaRole.Heading, new() { Name = english + tail, Exact = true });

    /// <summary>The accessible name of an element, as the page finds it: exact English, or English and Italian in an Italian run.</summary>
    protected static PageGetByRoleOptions Named(string name) =>
        PageText.IsItalian ? new() { NameRegex = PageText.Exact(name) } : new() { Name = name, Exact = true };

    /// <summary>The same for an element looked for inside another one.</summary>
    protected static LocatorGetByRoleOptions NamedIn(string name) =>
        PageText.IsItalian ? new() { NameRegex = PageText.Exact(name) } : new() { Name = name, Exact = true };

    /// <summary>
    /// Types into a field, replacing what it holds. In debug mode (HEADED=1) it types key by key so the
    /// run can be followed on screen.
    /// </summary>
    protected async Task TypeAsync(ILocator field, string text)
    {
        LogStep("Type", $"\"{text}\"");
        if (TestConfiguration.TypingDelay > 0)
        {
            await field.ClearAsync();
            await field.PressSequentiallyAsync(text, new() { Delay = TestConfiguration.TypingDelay });
        }
        else
        {
            await field.FillAsync(text);
        }
    }

    protected static void LogStep(string action, string? details = null)
    {
        Console.WriteLine(details != null ? $"  -> {action}: {details}" : $"  -> {action}");
    }
}
