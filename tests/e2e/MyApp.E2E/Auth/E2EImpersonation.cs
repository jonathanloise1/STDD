using Microsoft.Playwright;
using System.Text.Json;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Auth;

/// <summary>
/// E2E Impersonation Helper.
/// 
/// Provides authentication bypass for E2E tests by:
/// 1. Injecting mock user data into frontend localStorage (bypasses MSAL)
/// 2. Adding impersonation headers to API requests (backend accepts as authenticated)
/// 
/// This allows running E2E tests in CI/CD without manual OTP entry.
/// 
/// SECURITY NOTE: This only works when backend has MyApp:Impersonation:Enabled=true
/// which should NEVER be enabled in production environments.
/// </summary>
public static class E2EImpersonation
{
    /// <summary>
    /// LocalStorage key used by frontend to detect E2E mock mode.
    /// When present, frontend skips MSAL authentication entirely.
    /// </summary>
    public const string MockUserLocalStorageKey = "E2E_MOCK_USER";

    /// <summary>
    /// Configures a browser context for impersonated authentication.
    /// Sets up localStorage with mock user and intercepts API calls to add impersonation headers.
    /// </summary>
    /// <param name="context">Playwright browser context</param>
    /// <param name="user">Test user to impersonate</param>
    /// <param name="skipOnboardingBypass">If true, does NOT set the onboarding localStorage flag (for onboarding tests)</param>
    public static async Task SetupImpersonationAsync(IBrowserContext context, TestUser user, bool skipOnboardingBypass = false)
    {
        var headerName = TestConfiguration.ImpersonationHeaderName;
        
        // 1. Inject mock user into localStorage BEFORE any page loads
        // This script runs for every new page in the context
        var mockUserJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            id = user.Id,
            email = user.Email,
            role = user.Role.ToString().ToLowerInvariant(),
            isE2EMode = true
        });

        var onboardingLine = skipOnboardingBypass 
            ? "// Onboarding bypass SKIPPED for onboarding tests"
            : "localStorage.setItem('myapp_onboarded', 'true');";

        await context.AddInitScriptAsync($@"
            // E2E Mock Authentication Setup
            localStorage.setItem('{MockUserLocalStorageKey}', '{mockUserJson}');
            // Skip onboarding redirect for E2E tests (unless testing onboarding)
            {onboardingLine}
            // Force i18next to use English so text-based selectors match English translations
            localStorage.setItem('i18nextLng', 'en');
            console.log('[E2E] Mock user injected:', '{user.Email}');
        ");

        // 2. Intercept ALL API calls and add impersonation header
        await context.RouteAsync("**/api/**", async route =>
        {
            var headers = new Dictionary<string, string>(route.Request.Headers)
            {
                [headerName] = user.Id
            };

            // For auth/sync endpoint, override preferredLanguage to 'en'
            // so i18next uses English in E2E tests regardless of DB state
            if (route.Request.Url.Contains("/api/auth/sync"))
            {
                var response = await route.FetchAsync(new RouteFetchOptions { Headers = headers });
                var body = await response.TextAsync();

                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    // Clone and override preferredLanguage
                    using var ms = new System.IO.MemoryStream();
                    using (var writer = new Utf8JsonWriter(ms))
                    {
                        writer.WriteStartObject();
                        foreach (var prop in root.EnumerateObject())
                        {
                            if (prop.Name == "preferredLanguage")
                                writer.WriteString("preferredLanguage", "en");
                            else
                                prop.WriteTo(writer);
                        }
                        writer.WriteEndObject();
                    }
                    var patched = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                    await route.FulfillAsync(new RouteFulfillOptions
                    {
                        Response = response,
                        Body = patched
                    });
                }
                catch
                {
                    // If parsing fails, return original response
                    await route.FulfillAsync(new RouteFulfillOptions { Response = response });
                }
                return;
            }

            await route.ContinueAsync(new RouteContinueOptions
            {
                Headers = headers
            });
        });
        
        Console.WriteLine($"[E2E] Impersonation configured for user {user.Email} with header {headerName}");
    }

    /// <summary>
    /// Creates a new browser context with impersonation already configured.
    /// </summary>
    public static async Task<IBrowserContext> CreateImpersonatedContextAsync(
        IBrowser browser,
        TestUser user,
        bool skipOnboardingBypass = false)
    {
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            // Accept self-signed certificates for local development
            IgnoreHTTPSErrors = true,
            
            // Large viewport for better visibility during debugging
            // Set to common Full HD resolution
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            
            // Force English locale so i18next LanguageDetector picks 'en'
            // and text-based selectors in tests match English translations.
            Locale = "en-US"
        });

        await SetupImpersonationAsync(context, user, skipOnboardingBypass);

        return context;
    }
}
