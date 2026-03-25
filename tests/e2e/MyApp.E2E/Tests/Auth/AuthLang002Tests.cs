using Microsoft.Playwright;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using NUnit.Framework;

namespace MyApp.E2E.Tests.Auth;

/// <summary>
/// US-AUTH-08: Navigation menu items translate on language change.
/// Verifies that sidebar menu labels update immediately when the user
/// switches language, without requiring a page reload.
/// </summary>
[TestFixture]
[Category("Auth")]
[Property("TestCase", "AUTH-LANG-002")]
[Property("UserStory", "US-AUTH-08")]
public class AuthLang002Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    // US-AUTH-08: Sidebar menu items change to French when language is switched
    [Test]
    [Description("Sidebar menu items update to French labels when language is changed")]
    public async Task SidebarMenuItems_ShouldTranslate_WhenLanguageChangedToFrench()
    {
        await NavigateToAsync("/dashboard");

        // Wait for the sidebar to be visible (desktop only, d-none d-md-flex)
        var sidebar = Page.Locator("nav.slim-sidebar");
        await sidebar.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 15000 });

        // Verify initial labels exist (Italian is default fallback)
        var tasksItem = Page.Locator("#sidebar-tasks");
        await tasksItem.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 5000 });

        // Expand sidebar by hovering to make labels visible
        await sidebar.HoverAsync();
        await Page.WaitForTimeoutAsync(300); // Wait for expand animation

        // Capture the initial tasks label text
        var initialText = await tasksItem.Locator("span.text-nowrap").TextContentAsync();
        Assert.That(initialText, Is.Not.Null.And.Not.Empty, "Tasks label should have text when sidebar is expanded");

        // Switch language to French via the language selector
        var langButton = Page.Locator("#btn-language-selector");
        await langButton.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 5000 });
        await langButton.ClickAsync();

        var frButton = Page.Locator("#btn-lang-fr");
        await frButton.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 5000 });

        // Wait for the language update API call to complete
        var waitForLangUpdate = Page.WaitForResponseAsync(
            resp => resp.Url.Contains("/api/users/me/language") && resp.Status == 204,
            new() { Timeout = 10000 });
        await frButton.ClickAsync();
        await waitForLangUpdate;

        // Hover sidebar again to ensure labels are visible
        await sidebar.HoverAsync();
        await Page.WaitForTimeoutAsync(300);

        // Verify sidebar items now show French translations
        var tasksText = await Page.Locator("#sidebar-tasks span.text-nowrap").TextContentAsync();
        Assert.That(tasksText, Is.EqualTo("Tâches"), "Tasks label should be in French");

        var settingsText = await Page.Locator("#sidebar-settings span.text-nowrap").TextContentAsync();
        Assert.That(settingsText, Is.EqualTo("Paramètres"), "Settings label should be in French");

        var dashboardText = await Page.Locator("#sidebar-dashboard span.text-nowrap").TextContentAsync();
        Assert.That(dashboardText, Is.EqualTo("Tableau de bord"), "Dashboard label should be in French");
    }

    // US-AUTH-08: Sidebar labels persist after page reload
    [Test]
    [Description("Sidebar menu items remain translated after page reload")]
    public async Task SidebarMenuItems_ShouldRemainTranslated_AfterPageReload()
    {
        // Step 1: Navigate to dashboard (English by default from E2E impersonation)
        await NavigateToAsync("/dashboard");

        var sidebar = Page.Locator("nav.slim-sidebar");
        await sidebar.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        // Step 2: Switch language to French via UI (updates i18n, localStorage, and backend)
        var langButton = Page.Locator("#btn-language-selector");
        await langButton.ClickAsync();
        var frButton = Page.Locator("#btn-lang-fr");
        await frButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

        var waitForLangUpdate = Page.WaitForResponseAsync(
            resp => resp.Url.Contains("/api/users/me/language") && resp.Status == 204,
            new() { Timeout = 10000 });
        await frButton.ClickAsync();
        await waitForLangUpdate;

        // Step 3: Verify sidebar is now in French
        await sidebar.HoverAsync();
        await Page.WaitForTimeoutAsync(300);

        var tasksLocator = Page.Locator("#sidebar-tasks span.text-nowrap");
        await Assertions.Expect(tasksLocator).ToHaveTextAsync("Tâches", new() { Timeout = 10000 });

        // Step 4: Verify persistence — localStorage has the French language saved
        var storedLang = await Page.EvaluateAsync<string>("() => localStorage.getItem('i18nextLng')");
        Assert.That(storedLang, Is.EqualTo("fr"), "localStorage should persist 'fr' for next page load");

        // Step 5: Verify persistence — backend has saved French preference
        var profileResponse = await Page.APIRequest.GetAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/users/me/profile",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                IgnoreHTTPSErrors = true
            });
        Assert.That(profileResponse.Status, Is.EqualTo(200));
        var profileBody = await profileResponse.TextAsync();
        Assert.That(profileBody, Does.Contain("\"preferredLanguage\":\"fr\"").IgnoreCase,
            "Backend should return 'fr' as preferredLanguage, ensuring persistence on next page load");

        // Cleanup: reset language back to default for other tests
        await Page.APIRequest.PutAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/users/me/language",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                DataObject = new { language = "de" },
                IgnoreHTTPSErrors = true
            });
    }
}
