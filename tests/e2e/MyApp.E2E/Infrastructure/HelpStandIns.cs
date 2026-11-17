using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Playwright;

namespace MyApp.E2E.Infrastructure;

/// <summary>
/// The stand-ins of the help area: what the server tier and the help files answer when a test case needs a
/// given situation, installed as routes on one browser context so that no model is needed. Test classes never
/// call this class and never name a route: they call the methods of the area's base class
/// (<c>ServerAssistantDeclines()</c>, <c>TheHelpContentCannotBeLoaded()</c>), which call these.
///
/// A route installed later wins over an earlier one for the same address, so the base class installs the
/// default (no assistant writes answers) and a test case overrides it.
/// </summary>
public static class HelpStandIns
{
    private const string StatusUrl = "**/api/help/status";
    private const string AnswerUrl = "**/api/help/answer";
    private const string ManualFilesUrl = "**/help/*/manual.json";
    private const string PackFilesUrl = "**/help/*/pack.json";

    private const string Ndjson = "application/x-ndjson";
    private const string Json = "application/json";

    /// <summary>"No assistant writes answers": the status tells the panel to use retrieval only.</summary>
    public static Task AssistantWritesNoAnswers(IBrowserContext context) =>
        context.RouteAsync(StatusUrl, route => FulfillAsync(route, 200, Json,
            """{"order":"retrieval-only","compatible":true,"server":{"enabled":false,"available":false},"browser":{"enabled":false}}"""));

    /// <summary>The status of an assistant available on the server, tried before anything else.</summary>
    private static async Task ServerAssistantIsAvailable(IBrowserContext context)
    {
        await PackIsCompatibleWithTheAssistant(context, withBrowserModel: false);
        await context.RouteAsync(StatusUrl, route => FulfillAsync(route, 200, Json,
            """{"order":"server-first","compatible":true,"server":{"enabled":true,"available":true,"device":"cpu","timeout_seconds":20},"browser":{"enabled":false}}"""));
    }

    /// <summary>
    /// The pack the browser reads says it is compatible with the assistant. The pack built without a model lock is
    /// not compatible, which is right for a deployment (models.md 7.5); the tests of the panel's behaviour around
    /// answers run with stand-ins and no model, so in the test session only, the pack file is read from the
    /// frontend and answered with <c>assistant.compatible</c> true (and, for the browser tier, an
    /// <c>assistant.model</c> bound to the pack's own protocol hash). The compatibility rule itself is covered by
    /// unit tests (<c>engine.test.ts</c>, <c>help.test.ts</c>) and is untouched.
    /// </summary>
    private static Task PackIsCompatibleWithTheAssistant(IBrowserContext context, bool withBrowserModel) =>
        context.RouteAsync(PackFilesUrl, async route =>
        {
            if (route.Request.Method == "OPTIONS")
            {
                await FulfillAsync(route, 204, Json, "");
                return;
            }

            var response = await route.FetchAsync();
            var pack = JsonNode.Parse(await response.TextAsync())!.AsObject();
            var assistant = pack["assistant"]!.AsObject();
            assistant["compatible"] = true;
            if (withBrowserModel)
                assistant["model"] = new JsonObject
                {
                    ["id"] = "assistant",
                    ["version"] = "0.0.0-e2e",
                    ["protocol_sha256"] = assistant["protocol"]!["sha256"]!.GetValue<string>(),
                    ["retrieval_sha256"] = assistant["retrieval"]!["sha256"]!.GetValue<string>()
                };
            await FulfillAsync(route, 200, Json, pack.ToJsonString());
        });

    /// <summary>The server assistant writes <paramref name="answer"/> and cites these positions of its sections.</summary>
    public static async Task ServerAssistantAnswers(IBrowserContext context, string answer, params int[] cited)
    {
        await ServerAssistantIsAvailable(context);
        await AnswerStream(context, AnswerLines(answer, cited));
    }

    /// <summary>
    /// The server assistant writes <paramref name="answer"/> and cites the section titled
    /// <paramref name="sectionTitle"/> ("Create a task - Steps"). The position of a section in the question depends
    /// on the ranking, so it is read from the request: the sections the page sends, in order, found by the id the
    /// pack gives that title. When the page did not send the section, the answer is refused and the test fails
    /// on what the panel then shows.
    /// </summary>
    public static async Task ServerAssistantAnswersCiting(IBrowserContext context, string answer, string pageId)
    {
        await ServerAssistantIsAvailable(context);
        await context.RouteAsync(AnswerUrl, async route =>
        {
            if (route.Request.Method == "OPTIONS")
            {
                await FulfillAsync(route, 204, Json, "");
                return;
            }

            var sent = JsonNode.Parse(route.Request.PostData ?? "{}")?["section_ids"]?.AsArray()
                .Select(id => id!.GetValue<string>()).ToList() ?? new List<string>();
            // The first section of the page among those the panel sent (retrieval keeps one section per page).
            var position = sent.FindIndex(id => id.StartsWith(pageId + "#", StringComparison.Ordinal)) + 1;
            if (position == 0)
                await FulfillAsync(route, 400, "application/problem+json",
                    $$"""{"status":400,"title":"No section of the page \"{{pageId}}\" was among the sections sent."}""");
            else
                await FulfillAsync(route, 200, Ndjson, AnswerLines(answer, position));
        });
    }

    private static string AnswerLines(string answer, params int[] cited) =>
        Line(new { type = "delta", text = answer })
        + Line(new { type = "delta", text = "\nSource: " + string.Join(", ", cited.Select(n => $"[{n}]")) })
        + Line(new { type = "done", outcome = "answered", cited, body = answer });

    /// <summary>The server assistant writes <paramref name="text"/> and cites nothing.</summary>
    public static async Task ServerAssistantAnswersWithoutSource(IBrowserContext context, string text)
    {
        await ServerAssistantIsAvailable(context);
        await AnswerStream(context,
            Line(new { type = "delta", text }),
            Line(new { type = "done", outcome = "answered", cited = Array.Empty<int>(), body = text }));
    }

    /// <summary>The server assistant writes the sentence of the pack that declines.</summary>
    public static async Task ServerAssistantDeclines(IBrowserContext context, string refusalSentence)
    {
        await ServerAssistantIsAvailable(context);
        await AnswerStream(context,
            Line(new { type = "delta", text = refusalSentence }),
            Line(new { type = "done", outcome = "refused", cited = Array.Empty<int>() }));
    }

    /// <summary>The server assistant answers "too slow" (the reply the server gives when its limit is reached).</summary>
    public static async Task ServerAssistantIsTooSlow(IBrowserContext context)
    {
        await ServerAssistantIsAvailable(context);
        await context.RouteAsync(AnswerUrl, route => FulfillAsync(route, 504, "application/problem+json",
            """{"status":504,"title":"The assistant took too long to answer."}"""));
    }

    /// <summary>The manual bundle of every language cannot be loaded.</summary>
    public static Task ManualContentFails(IBrowserContext context) =>
        context.RouteAsync(ManualFilesUrl, route => FulfillAsync(route, 503, Json, "{}"));

    /// <summary>The help pack of every language cannot be loaded.</summary>
    public static Task PackFails(IBrowserContext context) =>
        context.RouteAsync(PackFilesUrl, route => FulfillAsync(route, 503, Json, "{}"));

    // ----- The browser tier (models.md 7): the device and the download, with no model -----

    /// <summary>The graphics adapter the stand-in device reports.</summary>
    public enum BrowserDevice
    {
        /// <summary>A real adapter that passes every step of the device check.</summary>
        Capable,

        /// <summary>A software adapter (SwiftShader), which the device check rejects.</summary>
        SoftwareAdapter
    }

    /// <summary>The path of the model file whose size stands for the download (<c>MODEL_FILE</c> of the worker).</summary>
    private const string ModelFileUrl = "**/onnx/model_q4f16.onnx";

    private const long Megabyte = 1024 * 1024;

    /// <summary>
    /// A device that can run the assistant in the browser: the status offers the browser tier first, the device
    /// reports <paramref name="device"/>, and the model file is <paramref name="downloadMb"/> MB large. No model
    /// exists and none is loaded.
    ///
    /// Three stand-ins make that work without a model:
    /// - the status route turns the browser tier on and sets the size above which the panel asks;
    /// - an init script gives the assistant worker a fake <c>navigator.gpu</c> (an init script does not run in
    ///   a worker, so the script wraps <c>Worker</c> and starts the real worker module after the fake);
    /// - a route answers the size request (HEAD) of the model file with the size.
    ///
    /// When <paramref name="downloadedMb"/> is given, a download is in progress: once the page asks the worker
    /// to load, the stand-in reports that many MB as loaded and nothing more. The panel asks before a download
    /// only when it is larger than <paramref name="askBeforeDownloadMb"/>.
    /// </summary>
    public static async Task BrowserAssistantOnDevice(
        IBrowserContext context, BrowserDevice device, int downloadMb, int askBeforeDownloadMb, int? downloadedMb = null)
    {
        await PackIsCompatibleWithTheAssistant(context, withBrowserModel: true);
        await context.RouteAsync(StatusUrl, route => FulfillAsync(route, 200, Json,
            JsonSerializer.Serialize(new
            {
                order = "browser-first",
                compatible = true,
                server = new { enabled = false, available = false },
                browser = new { enabled = true, ask_before_download_mb = askBeforeDownloadMb, timeout_seconds = 20 }
            })));

        await context.RouteAsync(ModelFileUrl, route =>
            route.Request.Method == "HEAD"
                ? route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    Headers = new Dictionary<string, string>
                    {
                        ["content-length"] = (downloadMb * Megabyte).ToString(),
                        ["content-type"] = "application/octet-stream",
                        ["cache-control"] = "no-store"
                    }
                })
                : route.AbortAsync());

        var settings = JsonSerializer.Serialize(new
        {
            software = device == BrowserDevice.SoftwareAdapter,
            downloadedBytes = downloadedMb is { } done ? done * Megabyte : (long?)null,
            totalBytes = downloadMb * Megabyte
        });
        await context.AddInitScriptAsync(DeviceInitScript.Replace("__SETTINGS__", settings));
    }

    /// <summary>
    /// Runs in every page. The assistant worker is started from a module that first defines the fake adapter (and,
    /// for a download in progress, answers the "load" message itself), then imports the real worker module.
    /// Static imports run in order, so the fake exists before the worker reads <c>navigator.gpu</c>.
    /// </summary>
    private const string DeviceInitScript = """
        (() => {
          const settings = __SETTINGS__;
          const fake = `
            const settings = ${JSON.stringify(settings)};
            const info = settings.software
              ? { vendor: 'google', architecture: 'swiftshader', device: 'SwiftShader', description: 'SwiftShader', isFallbackAdapter: true }
              : { vendor: 'e2e', architecture: 'e2e', device: 'E2E stand-in', description: 'E2E stand-in adapter', isFallbackAdapter: false };
            const adapter = {
              info,
              isFallbackAdapter: info.isFallbackAdapter,
              features: { has: () => true },
              limits: { maxBufferSize: 4294967296, maxStorageBufferBindingSize: 2147483648 }
            };
            Object.defineProperty(self.navigator, 'gpu', {
              configurable: true,
              value: { requestAdapter: async () => adapter }
            });
            Object.defineProperty(self.navigator, 'deviceMemory', { configurable: true, value: 8 });
            if (settings.downloadedBytes !== null) {
              self.addEventListener('message', (event) => {
                const message = event.data;
                if (!message || message.type !== 'load') return;
                event.stopImmediatePropagation();
                // Late enough for the panel to be listening for progress
                setTimeout(() => self.postMessage({
                  type: 'progress', id: message.id,
                  loadedBytes: settings.downloadedBytes, totalBytes: settings.totalBytes
                }), 1000);
              });
            }
          `;
          const fakeUrl = URL.createObjectURL(new Blob([fake], { type: 'text/javascript' }));
          const RealWorker = window.Worker;
          window.Worker = class extends RealWorker {
            constructor(url, options) {
              const href = new URL(url, document.baseURI).href;
              if (!href.includes('assistant.worker')) { super(url, options); return; }
              const entry = `import ${JSON.stringify(fakeUrl)}; import ${JSON.stringify(href)};`;
              super(URL.createObjectURL(new Blob([entry], { type: 'text/javascript' })), { ...options, type: 'module' });
            }
          };
        })();
        """;

    private static Task AnswerStream(IBrowserContext context, params string[] lines) =>
        context.RouteAsync(AnswerUrl, route => FulfillAsync(route, 200, Ndjson, string.Concat(lines)));

    private static string Line(object value) => JsonSerializer.Serialize(value) + "\n";

    /// <summary>
    /// Answers a request without reaching the backend. The page and the backend are different origins, so the
    /// answer carries the headers the browser needs, and a preflight is answered the same way.
    /// </summary>
    private static Task FulfillAsync(IRoute route, int status, string contentType, string body)
    {
        var request = route.Request;
        var origin = request.Headers.TryGetValue("origin", out var o) ? o : TestConfiguration.BaseUrl;
        var headers = new Dictionary<string, string>
        {
            ["access-control-allow-origin"] = origin,
            ["access-control-allow-credentials"] = "true",
            ["access-control-allow-methods"] = "GET,POST,OPTIONS",
            ["access-control-allow-headers"] =
                request.Headers.TryGetValue("access-control-request-headers", out var h) ? h : "*",
            ["cache-control"] = "no-store"
        };

        return request.Method == "OPTIONS"
            ? route.FulfillAsync(new RouteFulfillOptions { Status = 204, Headers = headers })
            : route.FulfillAsync(new RouteFulfillOptions
            {
                Status = status,
                ContentType = contentType,
                Headers = headers,
                Body = body
            });
    }
}
