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
/// 3. Adding the fault header when <see cref="FaultSwitch"/> asks for it
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

    private const string SessionStateCookie = "E2E_SESSION";
    private const string SessionUserCookie = "E2E_SESSION_USER";
    private const string SignedIn = "signed-in";
    private const string SignedOut = "signed-out";

    /// <summary>
    /// Ends the signed-in state of the session, as the identity provider does when the user signs out: from
    /// now on, whatever the pages open, the app finds no user. The application clears its own state at
    /// "Logout"; this is the part of signing out that lives in the identity provider.
    /// </summary>
    public static Task EndSessionAsync(IBrowserContext context) => SetSessionStateAsync(context, SignedOut);

    /// <summary>
    /// Makes the session signed in again, as completing the identity provider's sign-in does: the next
    /// address a page opens finds the user. Returns the email of the user the session was made for.
    /// </summary>
    public static async Task<string?> ResumeSessionAsync(IBrowserContext context)
    {
        await SetSessionStateAsync(context, SignedIn);
        var cookies = await context.CookiesAsync(new[] { TestConfiguration.BaseUrl });
        return cookies.FirstOrDefault(c => c.Name == SessionUserCookie)?.Value;
    }

    private static Task SetSessionStateAsync(IBrowserContext context, string state) =>
        context.AddCookiesAsync(new[]
        {
            new Cookie { Name = SessionStateCookie, Value = state, Url = TestConfiguration.BaseUrl }
        });

    /// <summary>
    /// Configures a browser context for impersonated authentication.
    /// Sets up localStorage with mock user and intercepts API calls to add impersonation headers.
    /// </summary>
    /// <param name="context">Playwright browser context</param>
    /// <param name="user">Test user to impersonate</param>
    /// <param name="startsSignedIn">
    /// True when the session starts signed in. False for a test case that starts "Not signed in": the
    /// identity provider cannot be driven, so the session holds the user's identity but does not use it until
    /// <see cref="ResumeSessionAsync"/> ("Sign in as ...").
    /// </param>
    /// <param name="keepsSavedLanguage">
    /// False replaces the language the backend returns at sign-in with English, so a language left behind by
    /// another run cannot change the text the tests look for. True for the test case that checks the language
    /// chosen earlier is kept at the next sign-in.
    /// </param>
    public static async Task SetupImpersonationAsync(
        IBrowserContext context, TestUser user, bool startsSignedIn = true, bool keepsSavedLanguage = false)
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

        // The session state is a cookie of the context, which the frontend's own origin and every page of
        // the context see: "signed-in" makes the frontend skip MSAL with the mock user, "signed-out" leaves it
        // with no user, as a visitor. It starts as startsSignedIn says and changes with EndSessionAsync and
        // ResumeSessionAsync, from whatever page the context shows.
        await context.AddCookiesAsync(new[]
        {
            new Cookie { Name = SessionStateCookie, Value = startsSignedIn ? SignedIn : SignedOut, Url = TestConfiguration.BaseUrl },
            new Cookie { Name = SessionUserCookie, Value = user.Email, Url = TestConfiguration.BaseUrl }
        });

        await context.AddInitScriptAsync($@"
            try {{
                if (document.cookie.split('; ').indexOf('{SessionStateCookie}={SignedIn}') >= 0) {{
                    localStorage.setItem('{MockUserLocalStorageKey}', '{mockUserJson}');
                }} else {{
                    localStorage.removeItem('{MockUserLocalStorageKey}');
                }}
                // Force i18next to use English so text-based selectors match English translations
                localStorage.setItem('i18nextLng', 'en');
            }} catch (e) {{ }}
        ");

        // 2. Intercept ALL API calls and add impersonation header
        await context.RouteAsync("**/api/**", async route =>
        {
            var headers = new Dictionary<string, string>(route.Request.Headers, StringComparer.OrdinalIgnoreCase)
            {
                [headerName] = user.Id
            };

            // The fault switch (development.md 16.2) rides on the same route: one route per context keeps
            // the order of handlers out of the picture.
            if (FaultSwitch.HeaderValueFor(context) is { } faultKeys)
                headers[TestConfiguration.FaultHeaderName] = faultKeys;

            // For auth/sync endpoint, override preferredLanguage to 'en'
            // so i18next uses English in E2E tests regardless of DB state
            if (!keepsSavedLanguage && route.Request.Url.Contains("/api/auth/sync"))
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
        bool startsSignedIn = true,
        bool keepsSavedLanguage = false)
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

        await SetupImpersonationAsync(context, user, startsSignedIn, keepsSavedLanguage);

        return context;
    }
}
