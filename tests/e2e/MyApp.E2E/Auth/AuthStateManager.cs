using Microsoft.Playwright;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Auth;

/// <summary>
/// Manages authentication state for E2E tests.
/// Implements Auth State Injection pattern to avoid repeated logins.
/// Supports Microsoft Entra External ID with OTP (email code) authentication.
/// </summary>
public class AuthStateManager
{
    private readonly string _stateDirectory;
    
    // Polling configuration
    private const int PollIntervalMs = 500;          // Check every 500ms
    private const int LoginTimeoutSeconds = 120;     // 2 minutes for OTP flow
    private const int StateValidityHours = 4;        // Reuse state for 4 hours

    public AuthStateManager(string baseDir)
    {
        _stateDirectory = Path.Combine(baseDir, ".auth");
        Directory.CreateDirectory(_stateDirectory);
    }

    /// <summary>
    /// Gets the path to the stored auth state for a user.
    /// </summary>
    public string GetStatePath(TestUser user)
        => Path.Combine(_stateDirectory, $"{user.Id}.json");

    /// <summary>
    /// Checks if a valid (non-expired) auth state exists.
    /// </summary>
    public bool HasValidState(TestUser user)
    {
        var path = GetStatePath(user);
        if (!File.Exists(path)) return false;

        var file = new FileInfo(path);
        // State is valid for configured hours
        return file.LastWriteTimeUtc > DateTime.UtcNow.AddHours(-StateValidityHours);
    }

    /// <summary>
    /// Creates an authenticated browser context for the given user.
    /// Performs real login if no valid state exists, otherwise reuses saved state.
    /// </summary>
    public async Task<IBrowserContext> CreateAuthenticatedContextAsync(
        IBrowser browser,
        TestUser user)
    {
        var statePath = GetStatePath(user);

        if (!HasValidState(user))
        {
            // Perform real login and save state
            await PerformLoginAndSaveStateAsync(browser, user, statePath);
        }

        // Create context with saved state
        return await browser.NewContextAsync(new BrowserNewContextOptions
        {
            StorageStatePath = statePath
        });
    }

    /// <summary>
    /// Performs login via Microsoft Entra External ID (OTP flow) and saves the auth state.
    /// If user is already logged in (from a previous session), saves that state directly.
    /// For OTP login, waits for user to complete manual code entry.
    /// </summary>
    private async Task PerformLoginAndSaveStateAsync(
        IBrowser browser,
        TestUser user,
        string statePath)
    {
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        try
        {
            Console.WriteLine($"[Auth] Navigating to {TestConfiguration.BaseUrl}...");
            await page.GotoAsync(TestConfiguration.BaseUrl);
            
            // Quick poll to check initial state
            var initialState = await PollForAuthStateAsync(page, 3000); // 3 second quick check
            
            if (initialState == AuthState.Authenticated)
            {
                Console.WriteLine($"[Auth] User already authenticated, saving session state");
                await context.StorageStateAsync(new BrowserContextStorageStateOptions
                {
                    Path = statePath
                });
                return;
            }

            if (initialState == AuthState.OnLoginPage)
            {
                // Start OTP login flow
                await PerformOtpLoginAsync(page, user);
            }

            // Poll until authenticated or timeout
            Console.WriteLine($"[Auth] Waiting for authentication to complete (polling every {PollIntervalMs}ms)...");
            var authenticated = await WaitForAuthenticationAsync(page, LoginTimeoutSeconds);

            if (!authenticated)
            {
                throw new TimeoutException($"Login did not complete within {LoginTimeoutSeconds} seconds");
            }

            Console.WriteLine($"[Auth] Authentication successful, saving state");
            
            // Wait a moment for app to fully initialize
            await Task.Delay(1000);
            
            // Save storage state
            await context.StorageStateAsync(new BrowserContextStorageStateOptions
            {
                Path = statePath
            });
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    /// <summary>
    /// Initiates the OTP login flow - enters email and submits.
    /// User must manually enter the OTP code received via email.
    /// </summary>
    private async Task PerformOtpLoginAsync(IPage page, TestUser user)
    {
        Console.WriteLine($"[Auth] Starting OTP login for {user.Email}...");
        Console.WriteLine($"[Auth] ⚠️  Check your email for the verification code and enter it in the browser!");

        // Try different selectors for email input (Entra External ID variants)
        var emailSelectors = new[]
        {
            "input[name='loginfmt']",
            "input[type='email']",
            "input[name='email']",
            "#email",
            "#signInName"
        };

        IElementHandle? emailInput = null;
        foreach (var selector in emailSelectors)
        {
            try
            {
                emailInput = await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
                {
                    Timeout = 2000,
                    State = WaitForSelectorState.Visible
                });
                if (emailInput != null)
                {
                    Console.WriteLine($"[Auth] Found email input: {selector}");
                    break;
                }
            }
            catch (TimeoutException)
            {
                continue;
            }
        }

        if (emailInput == null)
        {
            Console.WriteLine("[Auth] Could not find email input field - user may need to enter email manually");
            return;
        }

        // Fill email
        await emailInput.FillAsync(user.Email);
        Console.WriteLine($"[Auth] Email entered: {user.Email}");

        // Try different selectors for submit button
        var submitSelectors = new[]
        {
            "input[type='submit']",
            "button[type='submit']",
            "#next",
            "#idSIButton9",
            "button:has-text('Accedi')",
            "button:has-text('Sign in')",
            "button:has-text('Next')",
            "button:has-text('Avanti')"
        };

        foreach (var selector in submitSelectors)
        {
            try
            {
                var submitBtn = await page.QuerySelectorAsync(selector);
                if (submitBtn != null && await submitBtn.IsVisibleAsync())
                {
                    await submitBtn.ClickAsync();
                    Console.WriteLine($"[Auth] Clicked submit button: {selector}");
                    Console.WriteLine("[Auth] ========================================");
                    Console.WriteLine("[Auth] 📧 CHECK YOUR EMAIL FOR THE OTP CODE!");
                    Console.WriteLine("[Auth] Enter the code in the browser window.");
                    Console.WriteLine("[Auth] ========================================");
                    return;
                }
            }
            catch
            {
                continue;
            }
        }

        Console.WriteLine("[Auth] Could not find submit button - please click 'Sign in' manually");
    }

    /// <summary>
    /// Polls the page state to determine authentication status.
    /// </summary>
    private async Task<AuthState> PollForAuthStateAsync(IPage page, int timeoutMs)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var url = page.Url;
                
                // Check if on login page
                if (IsLoginPageUrl(url))
                {
                    return AuthState.OnLoginPage;
                }
                
                // Check if on app
                if (url.StartsWith(TestConfiguration.BaseUrl))
                {
                    // Verify we're actually authenticated (not just redirecting)
                    // Look for auth indicators in the page
                    var isAuthenticated = await CheckIfAuthenticatedAsync(page);
                    if (isAuthenticated)
                    {
                        return AuthState.Authenticated;
                    }
                }
                
                await Task.Delay(PollIntervalMs);
            }
            catch
            {
                await Task.Delay(PollIntervalMs);
            }
        }
        
        return AuthState.Unknown;
    }

    /// <summary>
    /// Waits for authentication to complete by polling.
    /// </summary>
    private async Task<bool> WaitForAuthenticationAsync(IPage page, int timeoutSeconds)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var lastStatus = "";
        
        while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
        {
            try
            {
                var url = page.Url;
                
                // Log status changes
                var currentStatus = GetStatusDescription(url);
                if (currentStatus != lastStatus)
                {
                    Console.WriteLine($"[Auth] Status: {currentStatus}");
                    lastStatus = currentStatus;
                }
                
                // Check if we're back on the app and authenticated
                if (url.StartsWith(TestConfiguration.BaseUrl) && !IsLoginPageUrl(url))
                {
                    var isAuthenticated = await CheckIfAuthenticatedAsync(page);
                    if (isAuthenticated)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Auth] Poll error: {ex.Message}");
            }
            
            await Task.Delay(PollIntervalMs);
        }
        
        return false;
    }

    /// <summary>
    /// Checks if the page indicates an authenticated user.
    /// </summary>
    private async Task<bool> CheckIfAuthenticatedAsync(IPage page)
    {
        try
        {
            // Check for common authenticated indicators
            // 1. Check URL is on app (not login page)
            if (IsLoginPageUrl(page.Url))
                return false;
            
            if (!page.Url.StartsWith(TestConfiguration.BaseUrl))
                return false;
            
            // 2. Check for authenticated UI elements (adjust selectors as needed)
            var authIndicators = new[]
            {
                "[data-testid='user-menu']",
                "[data-testid='logout-button']",
                ".user-avatar",
                "#user-menu",
                "nav[aria-label='User menu']"
            };
            
            foreach (var selector in authIndicators)
            {
                try
                {
                    var element = await page.QuerySelectorAsync(selector);
                    if (element != null && await element.IsVisibleAsync())
                    {
                        return true;
                    }
                }
                catch
                {
                    continue;
                }
            }
            
            // 3. If we're on the app and not on login, assume authenticated
            // after a brief network idle
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
            {
                Timeout = 2000
            });
            
            // Double-check we didn't redirect to login
            return !IsLoginPageUrl(page.Url) && page.Url.StartsWith(TestConfiguration.BaseUrl);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the URL is a login page URL.
    /// </summary>
    private bool IsLoginPageUrl(string url)
    {
        return url.Contains("loginappdev.MyApp.eu") ||
               url.Contains("login.microsoftonline.com") ||
               url.Contains("b2clogin.com") ||
               url.Contains("ciamlogin.com") ||
               url.Contains("/authorize") ||
               url.Contains("/oauth2/");
    }

    /// <summary>
    /// Gets a human-readable status description for logging.
    /// </summary>
    private string GetStatusDescription(string url)
    {
        if (url.Contains("loginappdev.MyApp.eu"))
            return "On Entra External ID login page";
        if (url.Contains("ciamlogin.com"))
            return "On CIAM login page";
        if (url.Contains("login.microsoftonline.com"))
            return "On Microsoft login page";
        if (url.Contains("/otp") || url.Contains("/code"))
            return "On OTP entry page";
        if (url.StartsWith(TestConfiguration.BaseUrl))
            return $"On app: {url}";
        return $"Unknown: {url}";
    }

    /// <summary>
    /// Clears stored auth state for a user.
    /// </summary>
    public void ClearState(TestUser user)
    {
        var path = GetStatePath(user);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Clears all stored auth states.
    /// </summary>
    public void ClearAllStates()
    {
        if (Directory.Exists(_stateDirectory))
        {
            foreach (var file in Directory.GetFiles(_stateDirectory, "*.json"))
            {
                File.Delete(file);
            }
        }
    }

    private enum AuthState
    {
        Unknown,
        OnLoginPage,
        Authenticated
    }
}
