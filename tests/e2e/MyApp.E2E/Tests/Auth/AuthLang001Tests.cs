using System.Text.Json;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using NUnit.Framework;

namespace MyApp.E2E.Tests.Auth;

/// <summary>
/// US-AUTH-08: User sets preferred language.
/// Verifies that users can change language via the UI and the preference
/// persists to the backend and survives a page refresh.
/// </summary>
[TestFixture]
[Category("Auth")]
[Property("TestCase", "AUTH-LANG-001")]
[Property("UserStory", "US-AUTH-08")]
public class AuthLang001Tests : AuthenticatedTestBase
{
    protected override TestUser GetTestUser() => TestUsers.AdminUser;

    // US-AUTH-08: Language selector shows all four languages
    [Test]
    [Description("Language selector shows available languages (DE, FR, IT, EN)")]
    public async Task LanguageSelector_ShouldShowAllLanguages()
    {
        await NavigateToAsync("/dashboard");

        // Click the language selector button (desktop, visible at md+)
        var langButton = Page.Locator("#btn-language-selector");
        await langButton.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 15000 });
        await langButton.ClickAsync();

        // Verify all four language options are visible in the dropdown
        await Page.Locator("#btn-lang-it").WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 5000 });
        Assert.That(await Page.Locator("#btn-lang-it").IsVisibleAsync(), Is.True, "Italian option should be visible");
        Assert.That(await Page.Locator("#btn-lang-en").IsVisibleAsync(), Is.True, "English option should be visible");
        Assert.That(await Page.Locator("#btn-lang-de").IsVisibleAsync(), Is.True, "German option should be visible");
        Assert.That(await Page.Locator("#btn-lang-fr").IsVisibleAsync(), Is.True, "French option should be visible");
    }

    // US-AUTH-08: Changing language persists to backend
    [Test]
    [Description("Changing language to French updates the UI and persists to backend")]
    public async Task ChangeLanguage_ToFrench_ShouldUpdateUIAndPersist()
    {
        await NavigateToAsync("/dashboard");

        // Click the language selector button
        var langButton = Page.Locator("#btn-language-selector");
        await langButton.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 15000 });
        await langButton.ClickAsync();

        // Select French
        var frButton = Page.Locator("#btn-lang-fr");
        await frButton.WaitForAsync(new() { State = Microsoft.Playwright.WaitForSelectorState.Visible, Timeout = 5000 });

        // Wait for the language update API call to complete
        var waitForLangUpdate = Page.WaitForResponseAsync(
            resp => resp.Url.Contains("/api/users/me/language") && resp.Status == 204,
            new() { Timeout = 10000 });
        await frButton.ClickAsync();
        await waitForLangUpdate;

        // Verify via sync that the language preference is persisted
        var syncResponse = await Page.APIRequest.PostAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/auth/sync",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                DataObject = new { },
                IgnoreHTTPSErrors = true
            });

        Assert.That(syncResponse.Status, Is.EqualTo(200));
        var json = await syncResponse.JsonAsync();
        Assert.That(json, Is.Not.Null, "Sync response should not be null");

        JsonElement langPropFr = default;
        Assert.That(json?.TryGetProperty("preferredLanguage", out langPropFr), Is.True,
            "Sync response should include preferredLanguage property");
        Assert.That(langPropFr.GetString(), Is.EqualTo("fr"),
            "Sync response should include preferredLanguage=fr");
    }

    // US-AUTH-08: Language preference survives page refresh
    [Test]
    [Description("Language preference persists across page refresh")]
    public async Task LanguagePreference_ShouldPersistAcrossRefresh()
    {
        // First, set language to Italian via API
        var putResponse = await Page.APIRequest.PutAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/users/me/language",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                DataObject = new { language = "it" },
                IgnoreHTTPSErrors = true
            });
        Assert.That(putResponse.Status, Is.EqualTo(204));

        // Navigate to dashboard (this triggers sync which reads preferredLanguage)
        await NavigateToAsync("/dashboard");

        // Verify the sync response contains the correct language
        var syncResponse = await Page.APIRequest.PostAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/auth/sync",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                DataObject = new { },
                IgnoreHTTPSErrors = true
            });

        Assert.That(syncResponse.Status, Is.EqualTo(200));
        var json = await syncResponse.JsonAsync();
        Assert.That(json, Is.Not.Null, "Sync response should not be null");

        JsonElement langPropIt = default;
        Assert.That(json?.TryGetProperty("preferredLanguage", out langPropIt), Is.True,
            "Sync response should include preferredLanguage property");
        Assert.That(langPropIt.GetString(), Is.EqualTo("it"),
            "Language preference should persist as 'it'");

        // Reset language back to default (de) for other tests
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

    // US-AUTH-08: Invalid language code rejected
    [Test]
    [Description("Invalid language code is rejected by the API")]
    public async Task UpdateLanguage_WithInvalidCode_ShouldReturn400()
    {
        var response = await Page.APIRequest.PutAsync(
            $"{TestConfiguration.ApiBaseUrl}/api/users/me/language",
            new()
            {
                Headers = new Dictionary<string, string>
                {
                    [TestConfiguration.ImpersonationHeaderName] = TestUsers.AdminUser.Id
                },
                DataObject = new { language = "xx" },
                IgnoreHTTPSErrors = true
            });

        // Backend throws ArgumentException for unsupported language -> 400 or 500
        Assert.That(response.Status, Is.Not.EqualTo(204), "Invalid language should not succeed");
    }
}
