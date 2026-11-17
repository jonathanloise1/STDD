using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using MyApp.E2E.Infrastructure;

namespace MyApp.E2E.Auth;

/// <summary>
/// Base class of every E2E test class that starts signed in.
///
/// Sign-in uses the impersonation pattern (development.md 16.1):
/// - Frontend: localStorage mock user, which makes the app skip MSAL.
/// - Backend: the impersonation header on every API call.
///
/// A test class gives the user ("Signed in as ..." of the test case) and then drives the screens through page
/// objects. It never calls the API, never reads the database and never writes a locator.
///
/// What the base class offers, in the words of a test case:
/// <list type="bullet">
/// <item><see cref="Page"/>: the session of the user, already signed in and showing the application.</item>
/// <item><see cref="StartSecondSession"/>: "A second session is signed in as ...".</item>
/// <item><see cref="ServiceDoesNotAnswer"/>: "The service does not answer when ...".</item>
/// <item><see cref="Unique"/>: a value no other run has used, for data the test creates.</item>
/// </list>
/// </summary>
[Parallelizable(ParallelScope.Self)]
public abstract class AuthenticatedTestBase : BrowserTest
{
    private readonly List<IBrowserContext> _extraContexts = new();

    protected TestUser CurrentUser { get; private set; } = null!;

    /// <summary>The page of the signed-in session. Pass it to page objects.</summary>
    protected IPage Page { get; private set; } = null!;

    /// <summary>Browser context of the signed-in session.</summary>
    protected IBrowserContext AuthContext { get; private set; } = null!;

    /// <summary>
    /// Unique identifier of this test run, used to make values unique.
    /// </summary>
    protected string TestRunId { get; private set; } = null!;

    /// <summary>
    /// The user the test is signed in as ("Signed in as {Role} of ..." in the test case).
    /// </summary>
    protected abstract TestUser GetTestUser();

    /// <summary>
    /// Whether setup opens the application after signing in. True for nearly every test case, whose first
    /// step is made on a page that is already open. A test case that starts "Not signed in" or needs a
    /// fault before the first load overrides it.
    /// </summary>
    protected virtual bool StartsInTheApplication => true;

    /// <summary>
    /// Whether the session starts signed in. A test case whose first precondition is "Not signed in"
    /// overrides it with false (see <see cref="VisitorTestBase"/>) and signs in with its own step.
    /// </summary>
    protected virtual bool StartsSignedIn => true;

    /// <summary>
    /// Whether the language the backend holds for the user is shown as it is. By default sign-in shows
    /// English whatever was saved, so a language left behind by another run changes nothing. The test case
    /// that checks a chosen language is kept at the next sign-in overrides it with true.
    /// </summary>
    protected virtual bool KeepsSavedLanguage => false;

    [SetUp]
    public async Task AuthSetup()
    {
        TestRunId = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
        CurrentUser = GetTestUser();

        AuthContext = await E2EImpersonation.CreateImpersonatedContextAsync(
            Browser, CurrentUser, StartsSignedIn, KeepsSavedLanguage);
        // Every session answers the identity provider's host itself: a user who signs out (or a visitor)
        // is sent there, and no real tenant exists to answer.
        await IdentityProviderStandIn.Install(AuthContext);
        Page = await AuthContext.NewPageAsync();
        Page.SetDefaultTimeout(TestConfiguration.Timeout);

        if (StartsInTheApplication)
            await OpenApplicationAsync(Page);

        Console.WriteLine($"[E2E] Signed in as {CurrentUser.Email}, run {TestRunId}");
    }

    [TearDown]
    public async Task AuthTeardown()
    {
        var result = TestContext.CurrentContext.Result;
        var status = result.Outcome.Status;
        var testName = TestContext.CurrentContext.Test.FullName;
        string? screenshotPath = null;

        if (status == TestStatus.Failed)
        {
            Console.WriteLine($"[E2E] FAILED: {testName}");
            Console.WriteLine($"[E2E]   Reason: {result.Message?.Split('\n').FirstOrDefault() ?? "Unknown error"}");
            screenshotPath = await CaptureScreenshotAsync();
        }
        else
        {
            Console.WriteLine($"[E2E] {status.ToString().ToUpperInvariant()}: {testName}");
        }

        TestResultTracker.Instance.Record(testName, status.ToString(), result.Message, screenshotPath);

        await CleanUpAsync();

        foreach (var context in _extraContexts)
            await context.CloseAsync();
        _extraContexts.Clear();

        if (AuthContext != null)
            await AuthContext.CloseAsync();
    }

    /// <summary>
    /// Restores what the test changed, through the UI (development.md 10.3). Runs after the result and the
    /// failure screenshot are taken and before the sessions close. Best effort: it must not fail the test.
    /// </summary>
    protected virtual Task CleanUpAsync() => Task.CompletedTask;

    /// <summary>
    /// A value that no other run has used: the text followed by a short run suffix. A test that creates data
    /// builds its titles and names with it, so repeated runs against the same database never collide
    /// (development.md 10.3).
    /// </summary>
    protected string Unique(string text) => $"{text} {TestRunId[^6..]}";

    /// <summary>
    /// "A second session is signed in as {Role} of ...": a second browser context, signed in as the user and
    /// showing the application. Steps that start with "As {Role}:" use the returned page.
    /// </summary>
    protected async Task<IPage> StartSecondSession(TestUser user)
    {
        var context = await E2EImpersonation.CreateImpersonatedContextAsync(Browser, user);
        _extraContexts.Add(context);
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(TestConfiguration.Timeout);
        await OpenApplicationAsync(page);
        Console.WriteLine($"[E2E] Second session signed in as {user.Email}");
        return page;
    }

    /// <summary>
    /// "The service does not answer when {situation}.": the operation of the Log key in the Observable trace
    /// of this test case fails for the signed-in session (development.md 16.2). The key comes from the test
    /// case file, never from the test class.
    /// </summary>
    protected void ServiceDoesNotAnswer()
    {
        var key = TestCaseDocs.LogKeyOf(TestCaseDocs.TestCaseIdOf(GetType()));
        FaultSwitch.Enable(AuthContext, key);
    }

    /// <summary>
    /// The operations answer again in the signed-in session. For clean-up after a test that used
    /// <see cref="ServiceDoesNotAnswer"/>.
    /// </summary>
    protected void ServiceAnswersAgain() => FaultSwitch.Disable(AuthContext);

    private static async Task OpenApplicationAsync(IPage page)
    {
        await page.GotoAsync(TestConfiguration.BaseUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task<string?> CaptureScreenshotAsync()
    {
        try
        {
            var screenshotDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "screenshots");
            Directory.CreateDirectory(screenshotDir);

            var screenshotPath = Path.Combine(
                screenshotDir,
                $"{TestContext.CurrentContext.Test.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png");

            await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });

            TestContext.AddTestAttachment(screenshotPath);
            return screenshotPath;
        }
        catch
        {
            return null;
        }
    }
}
