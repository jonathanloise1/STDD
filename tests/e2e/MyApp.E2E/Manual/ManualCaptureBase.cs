using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using MyApp.E2E.Auth;
using MyApp.E2E.Infrastructure;
using MyApp.E2E.PageObjects.Authentication;
using MyApp.E2E.PageObjects.Common;

namespace MyApp.E2E.Manual;

/// <summary>
/// Base class of the capture classes that take the screenshots of the user manual (manual.md 4,
/// development.md 10.4). A capture class has one <c>[Test]</c> per image; the method name is the state.
///
/// What it sets up, so a capture class only drives page objects and calls <see cref="ShootAsync"/>:
/// <list type="bullet">
/// <item>Normal runs: <c>E2E__ManualLanguage</c> not set, so every capture is skipped (Assert.Ignore).</item>
/// <item>One run per language (<c>en</c> or <c>it</c>): browser locale, UTC time zone, viewport 1280 x 800
/// (390 x 844 for a state whose name ends in <c>Mobile</c>), device scale factor 1.</item>
/// <item>Fixed clock: the browser clock at 2030-01-10T09:00:00Z; the run must also start the backend with
/// that instant (<c>E2E_FIXED_UTC_NOW</c>), which the base checks.</item>
/// <item>The user of the state, signed in by impersonation, the application already opened. In an Italian run
/// the application starts in Italian: the base answers the account request with Italian as the user's
/// language, without saving anything.</item>
/// <item>The shot: viewport only, animations off, caret hidden, fonts and network settled, the volatile regions
/// of the screen masked, the file written under docs/manual/{lang}/{area}/images/ and the manifest
/// docs/manual/captures.json updated.</item>
/// </list>
/// A capture never saves data: it may type into a form, but it stops before the request that would change
/// anything, or at a request the fault switch makes fail.
/// </summary>
public abstract class ManualCaptureBase : BrowserTest
{
    private const string FixedInstant = "2030-01-10T09:00:00Z";
    private const string MaskColor = "#D9D9D9";
    private const int SettleMilliseconds = 500;

    private static readonly object ManifestLock = new();
    private static readonly HashSet<string> ManifestStarted = new();

    private string _state = "";
    private string _viewport = "1280x800";

    /// <summary>"en" or "it": the language of this run.</summary>
    protected string Language { get; private set; } = "en";

    /// <summary>The page of the signed-in session. Pass it to page objects.</summary>
    protected IPage Page { get; private set; } = null!;

    private IBrowserContext CaptureContext { get; set; } = null!;

    /// <summary>
    /// The user the state is shot as. Most states use one user for the whole class; a state that needs another
    /// (an empty organization, a Viewer) says so by its name.
    /// </summary>
    protected abstract TestUser GetTestUser(string state);

    [OneTimeSetUp]
    public void ManualOneTimeSetUp()
    {
        var language = Environment.GetEnvironmentVariable("E2E__ManualLanguage");
        if (string.IsNullOrWhiteSpace(language))
            Assert.Ignore("Manual capture: set E2E__ManualLanguage to en or it to run it (manual.md 4.7).");

        if (language != "en" && language != "it")
            throw new InvalidOperationException($"E2E__ManualLanguage must be \"en\" or \"it\", not \"{language}\".");

        var expected = DateTimeOffset.Parse(FixedInstant, CultureInfo.InvariantCulture);
        if (TestConfiguration.FixedUtcNow is not { } configured ||
            !DateTimeOffset.TryParse(configured, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var actual) ||
            actual != expected)
        {
            throw new InvalidOperationException(
                $"A capture run needs the server clock at {FixedInstant}: set E2E_FIXED_UTC_NOW={FixedInstant} " +
                "(and start the backend with the same value when AutoStartApps is off).");
        }

        Language = language;
        PageText.Language = language;
    }

    [OneTimeTearDown]
    public void ManualOneTimeTearDown() => PageText.Language = "en";

    [SetUp]
    public async Task ManualSetUp()
    {
        _state = TestContext.CurrentContext.Test.MethodName!;
        var mobile = _state.EndsWith("Mobile", StringComparison.Ordinal);
        var viewport = mobile ? new ViewportSize { Width = 390, Height = 844 } : new ViewportSize { Width = 1280, Height = 800 };
        _viewport = $"{viewport.Width}x{viewport.Height}";
        var user = GetTestUser(_state);

        CaptureContext = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = viewport,
            DeviceScaleFactor = 1,
            Locale = Language == "it" ? "it-IT" : "en-US",
            TimezoneId = "UTC",
        });
        await CaptureContext.Clock.SetFixedTimeAsync(DateTime.Parse(FixedInstant, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal));
        // SetFixedTime fixes the date but leaves the page timers and animation frames stopped, so the page
        // transition of the application (a fade-in) never ends and the shot is a blank page. Resume the
        // timers: the date stays fixed.
        await CaptureContext.Clock.ResumeAsync();
        await E2EImpersonation.SetupImpersonationAsync(CaptureContext, user);
        if (Language == "it")
            await AnswerAccountRequestInItalianAsync(user);

        Page = await CaptureContext.NewPageAsync();
        Page.SetDefaultTimeout(TestConfiguration.Timeout);
        await Page.GotoAsync(TestConfiguration.BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    [TearDown]
    public async Task ManualTearDown()
    {
        if (CaptureContext != null)
            await CaptureContext.CloseAsync();
    }

    /// <summary>
    /// "The service does not answer when ...": the operation of the Log key of that test case fails for the
    /// session (development.md 16.2). Call it before the step that reaches the operation.
    /// </summary>
    protected void ServiceDoesNotAnswer(string testCaseId) =>
        FaultSwitch.Enable(CaptureContext, TestCaseDocs.LogKeyOf(testCaseId));

    /// <summary>Waits until the application shows a message (an error toast or an inline alert).</summary>
    protected Task WaitForMessageAsync() =>
        Assertions.Expect(new AddressPage(Page).Alerts.First).ToBeVisibleAsync();

    /// <summary>Shoots the viewport of the screen.</summary>
    protected Task ShootAsync(BasePage screen) => ShootCoreAsync(screen, null);

    /// <summary>Shoots one element of the screen, for example a dialog or a form.</summary>
    protected Task ShootElementAsync(BasePage screen, ILocator element) => ShootCoreAsync(screen, element);

    // ----- Language -----

    /// <summary>
    /// The application takes the user's language from the account request that follows sign-in. Sign-in by
    /// impersonation answers English whatever the user saved; for an Italian run the answer says Italian, so
    /// the application starts in Italian and nothing is written to the user's account.
    /// </summary>
    private async Task AnswerAccountRequestInItalianAsync(TestUser user)
    {
        await CaptureContext.RouteAsync(new Regex("/api/auth/sync"), async route =>
        {
            var headers = new Dictionary<string, string>(route.Request.Headers, StringComparer.OrdinalIgnoreCase)
            {
                [TestConfiguration.ImpersonationHeaderName] = user.Id
            };
            if (FaultSwitch.HeaderValueFor(CaptureContext) is { } faultKeys)
                headers[TestConfiguration.FaultHeaderName] = faultKeys;

            var response = await route.FetchAsync(new RouteFetchOptions { Headers = headers });
            if (response.Ok)
            {
                try
                {
                    if (JsonNode.Parse(await response.TextAsync()) is JsonObject account)
                    {
                        account["preferredLanguage"] = "it";
                        await route.FulfillAsync(new RouteFulfillOptions { Response = response, Body = account.ToJsonString() });
                        return;
                    }
                }
                catch (JsonException)
                {
                    // Not an account: pass the answer on as it is.
                }
            }
            await route.FulfillAsync(new RouteFulfillOptions { Response = response });
        });
    }

    /// <summary>
    /// Before the shot, the language button must show the run language. If it does not, the language is chosen
    /// through the language menu, as a user does. A screen with no language button is not checked.
    /// </summary>
    private async Task EnsureRunLanguageAsync()
    {
        var menus = new HeaderMenusPage(Page);
        var code = await menus.LanguageCode();
        if (code is null)
            return;

        var expected = Language.ToUpperInvariant();
        if (code != expected)
        {
            await menus.ClickLanguageButton();
            await menus.ClickLanguage(Language == "it" ? "Italiano" : "English");
            await Assertions.Expect(new AddressPage(Page).Alerts).ToHaveCountAsync(0);
        }
        await Assertions.Expect(menus.LanguageButton).ToHaveTextAsync(new Regex($"^{expected}$", RegexOptions.IgnoreCase));
    }

    // ----- The shot -----

    private async Task ShootCoreAsync(BasePage screen, ILocator? element)
    {
        await EnsureRunLanguageAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.EvaluateAsync("document.fonts.ready.then(() => true)");
        // The page and a dialog fade in when they open; a shot taken before the fade ends is blank or half drawn.
        await Page.WaitForFunctionAsync(
            "() => [...document.querySelectorAll('.page-wrapper, .modal')].every(e => getComputedStyle(e).opacity === '1')");
        // A message toast slides and fades in (spring animation of the application) and is counted as visible while
        // it is still transparent: a shot taken then shows an empty area where only the reference is masked.
        await Page.WaitForFunctionAsync(
            "() => [...document.querySelectorAll('.Toastify__toast-container [role=alert]')].every(a => { " +
            "for (let e = a; e && !e.classList.contains('Toastify__toast-container'); e = e.parentElement) " +
            "if (getComputedStyle(e).opacity !== '1') return false; return true; })");
        // Lets a dialog or a menu finish opening; animations are disabled for the shot itself.
        await Page.WaitForTimeoutAsync(SettleMilliseconds);

        var path = ImagePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // The reference of an error message changes on every request (operations.md 4.3), so it is always masked.
        var masks = screen.VolatileRegions
            .Append(Page.Locator("[role=alert] .small.text-muted > span"))
            .ToList();

        if (element is null)
        {
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = path,
                FullPage = false,
                Animations = ScreenshotAnimations.Disabled,
                Caret = ScreenshotCaret.Hide,
                Mask = masks,
                MaskColor = MaskColor,
            });
        }
        else
        {
            await element.ScreenshotAsync(new LocatorScreenshotOptions
            {
                Path = path,
                Animations = ScreenshotAnimations.Disabled,
                Caret = ScreenshotCaret.Hide,
                Mask = masks,
                MaskColor = MaskColor,
            });
        }

        RecordInManifest(path);
    }

    private string AreaPascal => GetType().Namespace!.Split('.').Last();

    private string ImagePath()
    {
        var screen = Kebab(Regex.Replace(GetType().Name, "Capture$", ""));
        var state = Kebab(_state);
        return Path.Combine(TestConfiguration.RepoRoot, "docs", "manual", Language, AreaPascal.ToLowerInvariant(), "images", $"{screen}-{state}.png");
    }

    private static string Kebab(string pascal) =>
        Regex.Replace(pascal, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();

    // ----- The manifest (manual.md 4.6) -----

    private void RecordInManifest(string imagePath)
    {
        var root = TestConfiguration.RepoRoot;
        var manifestPath = Path.Combine(root, "docs", "manual", "captures.json");
        var relative = Path.GetRelativePath(root, imagePath).Replace('\\', '/');
        var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(imagePath))).ToLowerInvariant();

        lock (ManifestLock)
        {
            JsonObject manifest = File.Exists(manifestPath) && JsonNode.Parse(File.ReadAllText(manifestPath)) is JsonObject existing
                ? existing
                : new JsonObject { ["schema_version"] = 1, ["images"] = new JsonArray() };
            var images = manifest["images"] as JsonArray ?? new JsonArray();

            // The first shot of a run replaces the entries of the run's language and keeps the others.
            var runKey = $"{manifestPath}|{Language}";
            if (ManifestStarted.Add(runKey))
            {
                foreach (var entry in images.Where(e => (string?)e?["lang"] == Language).ToList())
                    images.Remove(entry);
            }

            foreach (var entry in images.Where(e => (string?)e?["path"] == relative).ToList())
                images.Remove(entry);

            images.Add(new JsonObject
            {
                ["path"] = relative,
                ["lang"] = Language,
                ["capture"] = $"{AreaPascal}/{GetType().Name}.{_state}",
                ["viewport"] = _viewport,
                ["sha256"] = sha,
            });

            var sorted = new JsonArray();
            foreach (var entry in images.OrderBy(e => (string)e!["path"]!, StringComparer.Ordinal).ToList())
                sorted.Add(JsonNode.Parse(entry!.ToJsonString())!);
            manifest["images"] = sorted;

            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var text = manifest.ToJsonString(options).Replace("\r\n", "\n") + "\n";
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            File.WriteAllText(manifestPath, text, new UTF8Encoding(false));
        }
    }
}
