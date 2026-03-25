using Microsoft.Playwright;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.PageObjects.Common;

/// <summary>
/// Base class for all page objects.
/// Provides common utilities and selectors.
/// </summary>
public abstract class BasePage
{
    protected readonly IPage Page;
    
    /// <summary>
    /// Override to specify the page path (e.g., "/talent/profile")
    /// </summary>
    protected abstract string PagePath { get; }

    protected BasePage(IPage page)
    {
        Page = page;
    }

    /// <summary>
    /// Navigate to this page.
    /// </summary>
    public virtual async Task NavigateAsync()
    {
        Console.WriteLine($"  → Navigating to: {PagePath}");
        await Page.GotoAsync($"{TestConfiguration.BaseUrl}{PagePath}");
        await WaitForPageLoadAsync();
        Console.WriteLine($"  → Page loaded");
    }

    /// <summary>
    /// Wait for page to be fully loaded.
    /// </summary>
    public virtual async Task WaitForPageLoadAsync()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Common Selectors
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get element by HTML id attribute.
    /// </summary>
    protected ILocator GetById(string id)
        => Page.Locator($"#{id}");

    /// <summary>
    /// Get element by data-testid attribute (legacy, prefer GetById).
    /// </summary>
    protected ILocator GetByTestId(string testId)
        => Page.GetByTestId(testId);

    /// <summary>
    /// Get button by text.
    /// </summary>
    protected ILocator GetButton(string text)
        => Page.GetByRole(AriaRole.Button, new() { Name = text });

    /// <summary>
    /// Get link by text.
    /// </summary>
    protected ILocator GetLink(string text)
        => Page.GetByRole(AriaRole.Link, new() { Name = text });

    /// <summary>
    /// Get heading by text.
    /// </summary>
    protected ILocator GetHeading(string text)
        => Page.GetByRole(AriaRole.Heading, new() { Name = text });

    /// <summary>
    /// Get input by label.
    /// </summary>
    protected ILocator GetInputByLabel(string label)
        => Page.GetByLabel(label);

    // ═══════════════════════════════════════════════════════════════════════
    // Toast Notifications
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get toast message element.
    /// </summary>
    protected ILocator ToastMessage => GetByTestId("toast-message");

    /// <summary>
    /// Wait for and get toast message text.
    /// </summary>
    public async Task<string> GetToastMessageAsync()
    {
        await ToastMessage.WaitForAsync();
        return await ToastMessage.TextContentAsync() ?? "";
    }

    /// <summary>
    /// Assert toast contains expected text.
    /// </summary>
    public async Task AssertToastContainsAsync(string expectedText)
    {
        await Assertions.Expect(ToastMessage).ToContainTextAsync(expectedText);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Loading States
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wait for loading indicator to disappear.
    /// </summary>
    public async Task WaitForLoadingAsync()
    {
        var loader = GetByTestId("loading-spinner");
        await loader.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Text Input Helpers
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Log a test action step for visibility in console.
    /// </summary>
    protected void LogStep(string action, string? details = null)
    {
        var message = details != null ? $"  → {action}: {details}" : $"  → {action}";
        Console.WriteLine(message);
    }

    /// <summary>
    /// Type text into an input element.
    /// Uses configurable delay based on test mode:
    /// - Debug mode (browser visible): types character by character for visibility
    /// - CI/CD mode (headless): instant fill for speed
    /// </summary>
    /// <param name="locator">The input element to type into</param>
    /// <param name="text">The text to type</param>
    protected async Task TypeTextAsync(ILocator locator, string text)
    {
        var delay = TestConfiguration.TypingDelay;
        
        if (delay > 0)
        {
            LogStep("Typing (visible)", $"\"{text}\"");
            // Debug mode: type character by character for visibility
            await locator.PressSequentiallyAsync(text, new() { Delay = delay });
        }
        else
        {
            LogStep("Typing (fast)", $"\"{text}\"");
            // CI/CD mode: instant fill for speed
            await locator.FillAsync(text);
        }
    }
}
