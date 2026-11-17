using Microsoft.Playwright;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.PageObjects.Authentication;

/// <summary>
/// The keyboard shortcuts: pressing keys on a page and the "Keyboard Shortcuts" dialog.
/// </summary>
public sealed class ShortcutsPage : BasePage
{
    public ShortcutsPage(IPage page) : base(page) { }

    // ----- Actions -----

    /// <summary>"Press "G" then "T"": the two keys one after the other, with nothing focused.</summary>
    public async Task PressGThen(string key)
    {
        LogStep("Press", $"G then {key}");
        await Page.Keyboard.PressAsync("g");
        await Page.Keyboard.PressAsync(key);
    }

    /// <summary>"Press "?"".</summary>
    public async Task PressQuestionMark()
    {
        LogStep("Press", "?");
        await Page.Keyboard.PressAsync("?");
    }

    /// <summary>
    /// "Type "gd" in the "Title" field." Key by key, as a person types, so the keys reach the page the way
    /// shortcuts do.
    /// </summary>
    public async Task TypeInTitleField(string text)
    {
        LogStep("Type key by key", $"\"{text}\"");
        var field = Field("Title");
        await field.ClickAsync();
        await field.PressSequentiallyAsync(text);
    }

    // ----- State -----

    /// <summary>The "Keyboard Shortcuts" dialog.</summary>
    public ILocator Dialog => Page.GetByRole(AriaRole.Dialog, new() { Name = "Keyboard Shortcuts" });

    /// <summary>"Press G then a letter to navigate to a section."</summary>
    public ILocator Hint => Dialog.GetByText("Press G then a letter to navigate to a section.", new() { Exact = true });

    /// <summary>The row of a page in the dialog, for example "Team".</summary>
    public ILocator RowFor(string pageName) =>
        Dialog.GetByRole(AriaRole.Row).Filter(new() { HasText = pageName });
}
