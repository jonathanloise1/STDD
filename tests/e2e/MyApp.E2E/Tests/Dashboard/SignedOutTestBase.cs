using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Tests.Dashboard;

/// <summary>
/// Base class of a test case that starts "Not signed in": a browser session with no impersonation, so the
/// application treats the visitor as a stranger. It never calls the API and never writes a locator.
/// </summary>
[Parallelizable(ParallelScope.Self)]
public abstract class SignedOutTestBase : BrowserTest
{
    private IBrowserContext _context = null!;

    /// <summary>The page of the visitor.</summary>
    protected IPage Page { get; private set; } = null!;

    [SetUp]
    public async Task SignedOutSetup()
    {
        _context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            Locale = "en-US"
        });
        await IdentityProviderStandIn.Install(_context);
        Page = await _context.NewPageAsync();
        Page.SetDefaultTimeout(TestConfiguration.Timeout);
    }

    [TearDown]
    public async Task SignedOutTeardown()
    {
        if (_context != null)
            await _context.CloseAsync();
    }
}
