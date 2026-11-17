using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyApp.Monitor.Hosting;
using MyApp.Monitor.Investigator;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Store;
using MyApp.Monitor.Tools;
using NUnit.Framework;

namespace MyApp.Monitor.Tests;

public sealed class LogCapture : ILoggerProvider
{
    public sealed record Entry(LogLevel Level, string Category, string Name, string Message);

    private readonly List<Entry> _entries = [];
    public IReadOnlyList<Entry> Entries { get { lock (_entries) return _entries.ToList(); } }

    public ILogger CreateLogger(string categoryName) => new Capture(this, categoryName);
    public void Dispose() { }

    private sealed class Capture(LogCapture owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (owner._entries) owner._entries.Add(new Entry(logLevel, category, eventId.Name ?? "", formatter(state, exception)));
        }
    }
}

public sealed class RunObserver : IRunObserver
{
    public TaskCompletionSource<RunInfo> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Started_(RunInfo info) => Started.TrySetResult(info);
    void IRunObserver.Started(RunInfo info) => Started_(info);
}

/// <summary>
/// <c>run</c> started in this process on temporary files: the fixture pack, a log folder, a SQLite store, the dashboard on a loopback port. SQL,
/// the health endpoint, e-mail, the ticket webhook and the clock are fakes. Stopping lets the host shut down the way a service manager would.
/// </summary>
public sealed class RunHarness : IAsyncDisposable
{
    public const string Planted = "jane.doe@example.com";

    private readonly TempDir _dir;
    private readonly bool _ownsDir;
    private readonly FixtureRepo _repo;
    private readonly CancellationTokenSource _cts = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();
    private Task<int> _exit = Task.FromResult(-1);

    public FakeTime Time { get; }
    public FakeEmailSender Email { get; }
    public FakeWebhookClient Webhook { get; }
    public RecordingInvestigator Investigator { get; }
    public SqliteReadRunner Sql { get; }
    public LogCapture Logs { get; } = new();
    public RunObserver Observer { get; private set; } = new();
    public string Root => _dir.Path;
    public string LogDirectory => Path.Combine(_dir.Path, "logs");
    public string PackDirectory => Path.Combine(_dir.Path, "pack");
    public string DbPath => Path.Combine(_dir.Path, "data", "monitor.db");
    public HttpClient Client { get; private set; } = null!;
    public RunInfo Info { get; private set; } = null!;
    public int? ExitCode { get; private set; }
    public string Output => _out.ToString();
    public string ErrorOutput => _err.ToString();

    public Dictionary<string, string?> Settings { get; } = [];
    public Action<IServiceCollection>? ConfigureServices { get; set; }
    public MonitorHostOptions HostOptions { get; } = new() { MaintenanceInterval = TimeSpan.FromMilliseconds(200), ShutdownGrace = TimeSpan.FromSeconds(2) };
    public string[] ExtraArguments { get; set; } = [];
    public string Profile { get; set; } = "onprem";

    public RunHarness(Action<FixtureRepo>? tweakRepo = null, RunHarness? previous = null)
    {
        _repo = new FixtureRepo();
        // the fixture profile tails from the end every 5 seconds: the tests read from the beginning, every second
        _repo.Replace("docs/support/profiles/onprem.yaml", "start_at: end", "start_at: beginning");
        _repo.Replace("docs/support/profiles/onprem.yaml", "poll_seconds: 5", "poll_seconds: 1");
        tweakRepo?.Invoke(_repo);
        if (previous is null)
        {
            _dir = new TempDir();
            _ownsDir = true;
            Time = FakeTime.Default();
            Email = new FakeEmailSender();
            Webhook = new FakeWebhookClient();
            Investigator = new RecordingInvestigator(false);
            Sql = new SqliteReadRunner();
        }
        else
        {
            _dir = previous._dir;
            _ownsDir = false;
            Time = previous.Time;
            Email = previous.Email;
            Webhook = previous.Webhook;
            Investigator = previous.Investigator;
            Sql = previous.Sql;
            Logs = previous.Logs;
        }
        Directory.CreateDirectory(LogDirectory);
        var build = _repo.Build(outDir: Path.Combine(_dir.Path, "built"), zip: false);
        if (!build.Succeeded) throw new InvalidOperationException(string.Join("\n", build.Findings));
        if (Directory.Exists(PackDirectory)) Directory.Delete(PackDirectory, true);
        Copy(build.PackDirectory!, PackDirectory);
    }

    public string WriteLog(string name, params string[] lines)
    {
        var path = Path.Combine(LogDirectory, name);
        File.WriteAllText(path, string.Concat(lines.Select(l => l + "\n")));
        return path;
    }

    public void AppendLog(string name, params string[] lines) => File.AppendAllText(Path.Combine(LogDirectory, name), string.Concat(lines.Select(l => l + "\n")));

    public async Task StartAsync(bool expectStart = true)
    {
        Observer = new RunObserver();
        var config = new Dictionary<string, string?>
        {
            ["Monitor:Masking:HashKey"] = Fx.HashKey,
            ["Monitor:ContentRoot"] = _dir.Path,
            ["Monitor:Settings:LogGlob"] = Path.Combine(LogDirectory, "*.jsonl"),
            ["Monitor:Settings:ApiBaseUrl"] = "http://api.test",
            ["Monitor:Settings:EmailEnabled"] = "true",
            ["Monitor:Settings:TicketWebhookEnabled"] = "true",
            ["Monitor:Settings:TicketWebhookUrl"] = "http://tickets.test/api/tickets",
            ["Monitor:Settings:MailTo"] = "support@example.test",
            ["Monitor:Dashboard:Urls"] = "http://127.0.0.1:0",
            ["Monitor:Store:Path"] = DbPath,
        };
        foreach (var kv in Settings) config[kv.Key] = kv.Value;

        HostOptions.WatchPackPath ??= PackDirectory;
        var args = new List<string> { "run", "--pack", PackDirectory, "--profile", Profile };
        args.AddRange(ExtraArguments);
        _exit = Task.Run(async () =>
        {
            var code = await MonitorApp.RunAsync(args.ToArray(), _out, _err, services =>
            {
                services.AddLogging(b => b.AddProvider(Logs));
                services.AddSingleton<TimeProvider>(Time);
                services.AddSingleton<ISqlReadRunner>(Sql);
                services.AddSingleton<IHttpClientFactory>(FakeHttpFactory.Json(HttpStatusCode.OK, """{"status":"Healthy","checks":[{"name":"sqlserver","status":"Healthy","durationMs":3}]}"""));
                services.AddSingleton<IEmailSender>(Email);
                services.AddSingleton<ITicketWebhookClient>(Webhook);
                services.AddSingleton<IInvestigator>(Investigator);
                services.AddSingleton(new NotificationOptions { RetryDelays = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero] });
                services.AddSingleton(HostOptions);
                services.AddSingleton<IRunObserver>(Observer);
                ConfigureServices?.Invoke(services);
            }, config, _cts.Token);
            ExitCode = code;
            return code;
        });

        var first = await Task.WhenAny(Observer.Started.Task, _exit, Task.Delay(TimeSpan.FromSeconds(60)));
        if (first == Observer.Started.Task)
        {
            Info = await Observer.Started.Task;
            if (Info.DashboardUri is not null)
            {
                Client = new HttpClient(new HttpClientHandler { UseCookies = true, AllowAutoRedirect = true }) { BaseAddress = Info.DashboardUri };
                Client.Timeout = TimeSpan.FromSeconds(30);
            }
            return;
        }
        if (!expectStart) return;
        throw new InvalidOperationException($"the monitor did not start (exit {ExitCode}). stderr: {_err}\nlogs: {string.Join("\n", Logs.Entries.Where(e => e.Level >= LogLevel.Error).Select(e => e.Message))}");
    }

    public async Task<int> StopAsync()
    {
        _cts.Cancel();
        var done = await Task.WhenAny(_exit, Task.Delay(TimeSpan.FromSeconds(60)));
        if (done != _exit) throw new TimeoutException("the monitor did not stop in time");
        Client?.Dispose();
        return await _exit;
    }

    public Task<int> Exit => _exit;

    public RunHarness Next(Action<FixtureRepo>? tweakRepo = null)
    {
        var next = new RunHarness(tweakRepo, this) { Profile = Profile };
        foreach (var kv in Settings) next.Settings[kv.Key] = kv.Value;
        return next;
    }

    // ---- helpers for the tests ----

    public static async Task WaitForAsync(Func<Task<bool>> condition, string what, int seconds = 30)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end)
        {
            if (await condition()) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("timed out waiting for " + what);
    }

    public Task WaitForIncidentsAsync(int count, int seconds = 30) =>
        WaitForAsync(async () => (await Info.Store.ListAsync(new IncidentFilter())).Count >= count, $"{count} incident(s)", seconds);

    public async Task<string> GetAsync(string path)
    {
        using var response = await Client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.That((int)response.StatusCode, Is.EqualTo(200), $"GET {path}: {body[..Math.Min(body.Length, 400)]}");
        return body;
    }

    public async Task<HttpResponseMessage> PostFormAsync(string pagePath, string postPath, Dictionary<string, string> fields)
    {
        var page = await GetAsync(pagePath);
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.That(token, Is.Not.Empty, "the page carries an antiforgery token");
        var content = new FormUrlEncodedContent(fields.Append(KeyValuePair.Create("__RequestVerificationToken", token)));
        return await Client.PostAsync(postPath, content);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_cts.IsCancellationRequested) _cts.Cancel();
        try { await Task.WhenAny(_exit, Task.Delay(TimeSpan.FromSeconds(30))); } catch (Exception) { /* a failed start */ }
        Client?.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        _repo.Dispose();
        if (_ownsDir) _dir.Dispose();
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        foreach (var d in Directory.GetDirectories(from)) Copy(d, Path.Combine(to, Path.GetFileName(d)));
    }

    // ---- fixture log lines (operations.md 3.4) ----

    public static string UnavailableLine(string at = "2026-11-06T10:00:00.000Z", string message = "reported by " + Planted) =>
        $$"""{"Timestamp":"{{at}}","Level":"Error","Category":"MyApp.Application.Services.TaskItemService","EventId":2,"LogKey":"task.create.unavailable","MessageTemplate":"Task not created: the service failed for organization {OrganizationId}","Message":"Task not created: the service failed for organization {{Rig.OrgId}}; {{message}}","TraceId":"{{Rig.Trace}}","SpanId":"00f067aa0ba902b7","Properties":{"OrganizationId":"{{Rig.OrgId}}","Email":"{{Planted}}"},"Exception":{"Type":"System.TimeoutException","Message":"Timeout for {{Planted}}","Frames":["MyApp.Application.Services.TaskItemService.CreateAsync"]},"Service":"myapp-api","Version":"1.4.0","Host":"app01","Environment":"Production"}""";

    public static string AccessLostLine(int i) =>
        $$"""{"Timestamp":"2026-11-06T10:{{i / 60:D2}}:{{i % 60:D2}}.000Z","Level":"Warning","Category":"MyApp.Application.Services.TaskItemService","EventId":1,"LogKey":"task.create.access_lost","Message":"Task not created: user 6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72 has no access to organization {{Rig.OrgId}} ({{i}})","TraceId":"{{i + 1:x32}}","Properties":{"UserId":"6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72","OrganizationId":"{{Rig.OrgId}}"},"Version":"1.4.0"}""";

    public static string UnknownLine(string at = "2026-11-06T10:30:00.000Z", string message = "A widget exploded for <script>alert(1)</script>") =>
        $$"""{"Timestamp":"{{at}}","Level":"Error","Category":"Vendor.Widgets.Engine","Message":"{{message}}","Version":"1.4.0"}""";
}
