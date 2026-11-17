using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Dashboard;
using MyApp.Monitor.Hosting;
using MyApp.Monitor.Investigator;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Store;
using MyApp.Monitor.Tools;
using NUnit.Framework;

namespace MyApp.Monitor.Tests;

/// <summary>Builds the fixture repository's pack once for the whole run (it is read-only for the tests that share it).</summary>
[SetUpFixture]
public class SharedFixtures
{
    public static FixtureRepo Repo { get; private set; } = null!;
    public static OperationsPack Pack { get; private set; } = null!;

    [OneTimeSetUp]
    public void Build()
    {
        Repo = new FixtureRepo();
        Pack = Repo.BuildAndLoad();
    }

    [OneTimeTearDown]
    public void Cleanup() => Repo.Dispose();
}

public sealed class FakeEmailSender : IEmailSender
{
    public List<(SmtpConnection Connection, EmailMessage Message)> Sent { get; } = [];
    public Exception? Failure { get; set; }

    public Task SendAsync(SmtpConnection connection, EmailMessage message, CancellationToken ct)
    {
        if (Failure is not null) throw Failure;
        Sent.Add((connection, message));
        return Task.CompletedTask;
    }
}

public sealed class FakeWebhookClient : ITicketWebhookClient
{
    public List<WebhookRequest> Requests { get; } = [];
    /// <summary>Number of calls that fail (HTTP 503) before answers are good.</summary>
    public int FailFirst { get; set; }
    public Func<WebhookRequest, WebhookResponse> Respond { get; set; } = r => new WebhookResponse(201, "{\"id\":\"TCK-" + r.IdempotencyKey.Replace(':', '-') + "\"}");

    public Task<WebhookResponse> SendAsync(WebhookRequest request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(Requests.Count <= FailFirst ? new WebhookResponse(503, "unavailable") : Respond(request));
    }
}

public sealed class RecordingInvestigator(bool available) : IInvestigator
{
    public bool IsAvailable { get; set; } = available;
    public List<InvestigationRequest> Queued { get; } = [];

    public Task QueueAsync(InvestigationRequest request, CancellationToken ct)
    {
        Queued.Add(request);
        return Task.CompletedTask;
    }
}

/// <summary>
/// The monitor's production wiring on a temporary SQLite store, with every outside edge faked: SQL, the health endpoint, e-mail, the ticket
/// webhook, the investigator and the clock. Records go through the same pipeline, processor and dispatcher that <c>run</c> uses.
/// </summary>
public sealed class Rig : IDisposable
{
    public const string OrgId = "0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91";
    public const string Trace = "4bf92f3577b34da6a3ce929d0e0e4736";

    private readonly TempDir _dir;
    private readonly bool _ownsDir;
    private readonly ServiceProvider _services;
    private readonly OperationsPack _pack;
    private readonly Dictionary<string, string?>? _settings;
    private readonly bool _investigatorAvailable;
    private readonly bool _emailEnabled;
    private readonly bool _webhookEnabled;
    private readonly string _health;

    public FakeTime Time { get; } = FakeTime.Default();
    public SqliteStore Store { get; private set; }
    public FakeEmailSender Email { get; } = new();
    public FakeWebhookClient Webhook { get; } = new();
    public RecordingInvestigator Investigator { get; }
    public RunContext Context { get; }
    public IncidentProcessor Processor => _services.GetRequiredService<IncidentProcessor>();
    public NotificationDispatcher Notifier => _services.GetRequiredService<NotificationDispatcher>();
    public IncidentActions Actions => _services.GetRequiredService<IncidentActions>();
    public SuppressionService Suppression => _services.GetRequiredService<SuppressionService>();
    public DigestService Digest => _services.GetRequiredService<DigestService>();
    public RunContextHolder Holder => _services.GetRequiredService<RunContextHolder>();
    public IServiceProvider Services => _services;
    public string LogDirectory { get; }
    public string DbPath { get; }
    public FakeHttpFactory Http { get; }
    public SqliteReadRunner Sql { get; } = new();

    public Rig(
        OperationsPack? pack = null, Dictionary<string, string?>? settings = null, bool investigatorAvailable = false, bool emailEnabled = true,
        bool webhookEnabled = true, string health = """{"status":"Healthy","checks":[{"name":"sqlserver","status":"Healthy","durationMs":3}]}""",
        TempDir? sharedDir = null, DateTimeOffset? start = null, NotificationOptions? notifications = null, Action<IServiceCollection>? configure = null)
    {
        pack ??= SharedFixtures.Pack;
        _pack = pack;
        _settings = settings;
        _investigatorAvailable = investigatorAvailable;
        _emailEnabled = emailEnabled;
        _webhookEnabled = webhookEnabled;
        _health = health;
        _dir = sharedDir ?? new TempDir();
        _ownsDir = sharedDir is null;
        if (start is not null) Time.Set(start.Value);
        LogDirectory = Path.Combine(_dir.Path, "logs");
        Directory.CreateDirectory(LogDirectory);
        DbPath = Path.Combine(_dir.Path, "data", "monitor.db");
        Investigator = new RecordingInvestigator(investigatorAvailable);
        Http = FakeHttpFactory.Json(HttpStatusCode.OK, health);

        var config = new Dictionary<string, string?>
        {
            ["Monitor:Masking:HashKey"] = Fx.HashKey,
            ["Monitor:Settings:LogGlob"] = Path.Combine(LogDirectory, "*.jsonl"),
            ["Monitor:Settings:ApiBaseUrl"] = "http://api.test",
            ["Monitor:Settings:EmailEnabled"] = emailEnabled ? "true" : "false",
            ["Monitor:Settings:TicketWebhookEnabled"] = webhookEnabled ? "true" : "false",
            ["Monitor:Settings:TicketWebhookUrl"] = "http://tickets.test/api/tickets",
            ["Monitor:Settings:MailTo"] = "support@example.test",
            ["Monitor:ContentRoot"] = _dir.Path,
        };
        foreach (var kv in settings ?? []) config[kv.Key] = kv.Value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(config).Build();

        Store = new SqliteStore(SqliteDatabase.Open(DbPath));
        var services = new ServiceCollection();
        services.AddLogging();
        MonitorApp.AddMonitor(services, configuration);
        services.AddMonitorRun(Store, new MonitorHostOptions(), "http://localhost:5310");
        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton<ISqlReadRunner>(Sql);
        services.AddSingleton<IHttpClientFactory>(Http);
        services.AddSingleton<IEmailSender>(Email);
        services.AddSingleton<ITicketWebhookClient>(Webhook);
        services.AddSingleton<IInvestigator>(Investigator);
        services.AddSingleton(notifications ?? new NotificationOptions { Background = false, RetryDelays = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero] });
        configure?.Invoke(services);   // last, so a test can replace any registration (the investigator host, for one)
        _services = services.BuildServiceProvider();

        Context = _services.GetRequiredService<RunContextFactory>().Create(pack);
        Holder.Replace(Context);
        Suppression.Configure(Context.Profile.Suppression);
    }

    public IIncidentStore Incidents => Store;

    /// <summary>One batch through the pipeline and the processor, as the host does it; the results are returned.</summary>
    public async Task<IReadOnlyList<AnalysisResult>> FeedAsync(params LogRecord[] records)
    {
        var results = await Context.Pipeline.ProcessAsync(new SignalBatch("applogs", records, null), CancellationToken.None);
        foreach (var r in results) await Processor.ProcessAsync(Context, r);
        return results;
    }

    public LogRecord Keyed(
        string key = "task.create.access_lost", RecordLevel level = RecordLevel.Warning, string? message = null, DateTimeOffset? at = null,
        string? trace = Trace, string org = OrgId, string version = "1.4.0")
    {
        var tail = key == "task.create.access_lost" ? $"Task not created: user 6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72 has no access to organization {org}" : $"Task not created: the service failed for organization {org}";
        return Fx.Record(
            message ?? tail, level, eventName: key, category: "MyApp.Application.Services.TaskItemService", at: at ?? Time.GetUtcNow(), traceId: trace,
            props: new Dictionary<string, string> { ["OrganizationId"] = org, ["UserId"] = "6f1c2a90-3b7d-4e51-a8c2-9d0e4f6b1a72" }, version: version, source: "applogs");
    }

    public static LogRecord Health(string check = "sqlserver", DateTimeOffset? at = null) => Fx.Record(
        message: $"Health check {check} is Unhealthy", level: RecordLevel.Error, eventName: "platform.health.unhealthy", category: "MyApp.WebApi.Health",
        at: at, props: new Dictionary<string, string> { ["Check"] = check }, source: "applogs");

    public static LogRecord Unknown(string message = "A widget exploded", RecordLevel level = RecordLevel.Error, DateTimeOffset? at = null) => Fx.Record(
        message: message, level: level, category: "Vendor.Widgets.Engine", at: at, source: "applogs");

    public async Task<IReadOnlyList<IncidentRow>> AllIncidentsAsync() => await Store.ListAsync(new IncidentFilter(), 500);

    public void Dispose()
    {
        _services.Dispose();
        Store.Database.Close();
        if (_ownsDir) _dir.Dispose();
    }

    /// <summary>A new monitor process on the same files and the same store: nothing in memory survives, the clock continues.</summary>
    public Rig Restart()
    {
        Store.Database.Close();
        return new Rig(_pack, _settings, _investigatorAvailable, _emailEnabled, _webhookEnabled, _health, _dir, Time.GetUtcNow());
    }

    /// <summary>The store read through a fresh connection after a restart.</summary>
    public SqliteStore Reopen()
    {
        Store.Database.Close();
        Store = new SqliteStore(SqliteDatabase.Open(DbPath));
        return Store;
    }
}
