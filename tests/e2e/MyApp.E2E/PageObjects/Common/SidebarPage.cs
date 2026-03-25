using Microsoft.Playwright;
using NUnit.Framework;

namespace MyApp.E2E.PageObjects.Common;

/// <summary>
/// Page object for sidebar navigation interactions.
/// 
/// Menu items render with IDs in the format: {rootId}__{menuId}--link
/// Submenu containers render as: {rootId}__{menuId}
/// </summary>
public class SidebarPage
{
    private readonly IPage _page;

    public SidebarPage(IPage page)
    {
        _page = page;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Locators
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The main sidebar/aside navigation container.
    /// </summary>
    public ILocator Sidebar => _page.Locator("aside nav, aside .navigation, [class*='aside']").First;

    /// <summary>
    /// Get a sidebar menu item link by its translated text.
    /// </summary>
    public ILocator GetMenuItemByText(string text)
        => _page.Locator($"aside a, aside [class*='navigation-link']")
            .Filter(new LocatorFilterOptions { HasText = text });

    /// <summary>
    /// Get a sidebar menu item by its unique ID attribute.
    /// Matches elements whose id ends with the given menuId--link.
    /// </summary>
    public ILocator GetMenuItemById(string menuId)
        => _page.Locator($"[id$='{menuId}--link']");

    /// <summary>
    /// Get the submenu container by its unique menu ID.
    /// Matches elements whose id ends with the given menuId.
    /// </summary>
    public ILocator GetSubmenuById(string menuId)
        => _page.Locator($"[id$='{menuId}']:not([id$='--link'])");

    /// <summary>
    /// Check if a menu item has a lock icon (feature-gated).
    /// Matches elements whose id ends with {menuId}--lock.
    /// </summary>
    public ILocator GetLockedIcon(string menuId)
        => _page.Locator($"[id$='{menuId}--lock']");

    /// <summary>
    /// The "Back to Dashboard" button in settings area.
    /// Matches elements whose id ends with 'settings-back-to-dashboard'.
    /// </summary>
    public ILocator BackToDashboardLink
        => _page.Locator("[id$='settings-back-to-dashboard']");

    // ═══════════════════════════════════════════════════════════════════════
    // Actions
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Click a menu item by its translated text label.
    /// </summary>
    public async Task ClickMenuItemAsync(string text)
    {
        var item = GetMenuItemByText(text);
        await item.First.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Click a menu item by its unique ID.
    /// </summary>
    public async Task ClickMenuItemByIdAsync(string menuId)
    {
        var item = GetMenuItemById(menuId);
        await item.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Click the "Back to Dashboard" link in settings.
    /// </summary>
    public async Task ClickBackToDashboardAsync()
    {
        await BackToDashboardLink.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Assertions
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Assert that the sidebar contains a menu item with the given text.
    /// </summary>
    public async Task AssertMenuItemVisibleAsync(string text)
    {
        await Assertions.Expect(GetMenuItemByText(text).First).ToBeVisibleAsync();
    }

    /// <summary>
    /// Assert that the sidebar does NOT contain a menu item with the given text.
    /// </summary>
    public async Task AssertMenuItemNotVisibleAsync(string text)
    {
        await Assertions.Expect(GetMenuItemByText(text).First).Not.ToBeVisibleAsync();
    }

    /// <summary>
    /// Assert that a menu item has a lock icon (feature-gated, subscription not included).
    /// </summary>
    public async Task AssertMenuItemLockedAsync(string menuId)
    {
        await Assertions.Expect(GetLockedIcon(menuId)).ToBeVisibleAsync();
    }

    /// <summary>
    /// Assert that a menu item does NOT have a lock icon.
    /// </summary>
    public async Task AssertMenuItemUnlockedAsync(string menuId)
    {
        await Assertions.Expect(GetLockedIcon(menuId)).Not.ToBeVisibleAsync();
    }

    /// <summary>
    /// Assert the current URL matches the expected path.
    /// </summary>
    public async Task AssertUrlContainsAsync(string expectedPath)
    {
        await Assertions.Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(
            System.Text.RegularExpressions.Regex.Escape(expectedPath)));
    }

    /// <summary>
    /// Get all visible menu item texts from the sidebar.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetVisibleMenuItemTextsAsync()
    {
        var items = _page.Locator("aside [class*='navigation-link'] span[class*='text']");
        return await items.AllTextContentsAsync();
    }

    /// <summary>
    /// Assert that a menu item is currently active/selected (highlighted).
    /// Active items typically have an 'active' or 'selected' CSS class.
    /// </summary>
    public async Task AssertMenuItemActiveAsync(string menuId)
    {
        var item = GetMenuItemById(menuId);
        // Active items usually include an 'active' class or aria-current
        await Assertions.Expect(item).ToHaveAttributeAsync(
            "class",
            new System.Text.RegularExpressions.Regex("active|selected|current"));
    }

    /// <summary>
    /// Assert that the sidebar menu items appear in the expected order.
    /// Matches top-level item texts in sequence.
    /// </summary>
    public async Task AssertMenuOrderAsync(IReadOnlyList<string> expectedTexts)
    {
        var visibleTexts = await GetVisibleMenuItemTextsAsync();
        var filtered = visibleTexts
            .Where(t => expectedTexts.Contains(t, StringComparer.OrdinalIgnoreCase))
            .ToList();

        for (int i = 0; i < expectedTexts.Count; i++)
        {
            Assert.That(
                filtered.Count > i && string.Equals(filtered[i], expectedTexts[i], StringComparison.OrdinalIgnoreCase),
                Is.True,
                $"Expected menu item at position {i} to be '{expectedTexts[i]}', but was '{(filtered.Count > i ? filtered[i] : "<missing>")}'");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Responsive / Collapse Actions
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The collapse/expand toggle button on desktop sidebar.
    /// </summary>
    public ILocator CollapseToggle
        => _page.Locator("aside [class*='collapse'], aside [class*='toggle'], aside button[aria-label*='collapse'], aside button[aria-label*='menu']").First;

    /// <summary>
    /// The hamburger menu button visible on mobile viewports.
    /// </summary>
    public ILocator HamburgerMenu
        => _page.Locator("header button[aria-label*='menu'], [class*='hamburger'], button[class*='mobile-menu']").First;

    /// <summary>
    /// Click the sidebar collapse/expand toggle button.
    /// </summary>
    public async Task ClickCollapseToggleAsync()
    {
        await CollapseToggle.ClickAsync();
        await _page.WaitForTimeoutAsync(300); // Wait for animation
    }

    /// <summary>
    /// Click the hamburger menu button on mobile.
    /// </summary>
    public async Task ClickHamburgerMenuAsync()
    {
        await HamburgerMenu.ClickAsync();
        await _page.WaitForTimeoutAsync(300); // Wait for overlay animation
    }

    /// <summary>
    /// Assert that the sidebar is in collapsed state (icons only, no labels).
    /// </summary>
    public async Task AssertSidebarCollapsedAsync()
    {
        var aside = _page.Locator("aside").First;
        await Assertions.Expect(aside).ToHaveAttributeAsync(
            "class",
            new System.Text.RegularExpressions.Regex("collapsed|minimized|mini"));
    }

    /// <summary>
    /// Assert that the sidebar is in expanded state (full width with labels).
    /// </summary>
    public async Task AssertSidebarExpandedAsync()
    {
        var aside = _page.Locator("aside").First;
        // Should NOT have collapsed class
        await Assertions.Expect(aside).Not.ToHaveAttributeAsync(
            "class",
            new System.Text.RegularExpressions.Regex("collapsed|minimized|mini"));
    }

    /// <summary>
    /// Assert that the sidebar is hidden on mobile (overlay closed).
    /// </summary>
    public async Task AssertSidebarHiddenOnMobileAsync()
    {
        var aside = _page.Locator("aside").First;
        await Assertions.Expect(aside).ToBeHiddenAsync();
    }

    /// <summary>
    /// Assert that the sidebar is visible (overlay open or desktop visible).
    /// </summary>
    public async Task AssertSidebarVisibleAsync()
    {
        var aside = _page.Locator("aside").First;
        await Assertions.Expect(aside).ToBeVisibleAsync();
    }
}
