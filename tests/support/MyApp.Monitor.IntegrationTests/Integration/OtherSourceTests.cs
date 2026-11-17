using System.Net;
using System.Text.Json.Nodes;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Tools;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

public class LogAnalyticsSourceTests
{
    private FakeTime _time = null!;
    private InMemorySourceStateStore _state = null!;
    private FakeLogAnalyticsClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _time = FakeTime.Default();
        _state = new InMemorySourceStateStore();
        _client = new FakeLogAnalyticsClient();
    }

    private static JsonObject Settings(Action<JsonObject>? change = null)
    {
        var s = new JsonObject
        {
            ["workspace_id"] = "ws-1", ["app_role_names"] = new JsonArray("myapp-api"), ["min_level"] = 2, ["poll_seconds"] = 60,
            ["lookback_minutes_first_run"] = 30, ["max_rows_per_poll"] = 3,
        };
        change?.Invoke(s);
        return s;
    }

    private async Task<LogAnalyticsSource> Start(JsonObject? settings = null, IWorkspaceResolver? resolver = null)
    {
        var source = new LogAnalyticsSource("appinsights", _client, resolver ?? new FakeWorkspaceResolver());
        await source.StartAsync(new SourceContext("appinsights", "log_analytics", "prod", settings ?? Settings(), "/", _state, _time), default);
        return source;
    }

    private static Dictionary<string, object?> Row(string ingested, string type = "AppTraces", int severity = 2, string message = "m", string props = "{}", string operationId = "4bf92f3577b34da6a3ce929d0e0e4736") => new()
    {
        ["IngestedAt"] = DateTimeOffset.Parse(ingested), ["TimeGenerated"] = DateTimeOffset.Parse(ingested).AddSeconds(-1), ["Type"] = type, ["SeverityLevel"] = severity,
        ["Message"] = message, ["ExceptionType"] = null, ["OuterMessage"] = null, ["Method"] = null, ["OperationId"] = operationId, ["ParentId"] = "00f067aa0ba902b7",
        ["AppVersion"] = "1.4.0", ["AppRoleInstance"] = "app01", ["Properties"] = props,
    };

    [Test]
    public async Task The_query_has_the_specified_shape()
    {
        var source = await Start();
        var q = source.BuildQuery(new DateTimeOffset(2026, 11, 6, 11, 30, 0, TimeSpan.Zero), _time.GetUtcNow());
        Assert.That(q, Does.Contain("union AppTraces, AppExceptions"));
        Assert.That(q, Does.Contain("ingestion_time() > datetime(2026-11-06T11:30:00.0000000Z) and ingestion_time() <= datetime(2026-11-06T12:00:00.0000000Z)"));
        Assert.That(q, Does.Contain("AppRoleName in ('myapp-api')"));
        Assert.That(q, Does.Contain("SeverityLevel >= 2 or isnotempty(tostring(Properties.LogKey))"));
        Assert.That(q, Does.Contain("order by IngestedAt asc"));
        Assert.That(q, Does.Contain("take 3"));
    }

    [Test]
    public async Task The_first_run_starts_at_now_minus_the_lookback_and_uses_the_workspace()
    {
        var source = await Start();
        await source.RunOnceAsync(default);
        var (workspace, query) = _client.Calls.Single();
        Assert.That(workspace, Is.EqualTo("ws-1"));
        Assert.That(query, Does.Contain("datetime(2026-11-06T11:30:00.0000000Z)"));
    }

    [Test]
    public async Task Rows_are_mapped_to_the_normal_form()
    {
        _client.Responses.Enqueue([Row("2026-11-06T11:59:00Z", props: """
            {"LogKey":"task.create.access_lost","CategoryName":"MyApp.Services.TaskItemService","{OriginalFormat}":"Task not created: {OrganizationId}","OrganizationId":"0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91"}
            """, message: "Task not created: 0b7d...")]);
        var source = await Start();
        var result = await source.RunOnceAsync(default);
        var r = result.Batches.Single().Records.Single();
        Assert.That(r.SourceId, Is.EqualTo("appinsights"));
        Assert.That(r.Level, Is.EqualTo(RecordLevel.Warning));
        Assert.That(r.LogKey, Is.EqualTo("task.create.access_lost"));
        Assert.That(r.EventName, Is.EqualTo("task.create.access_lost"));
        Assert.That(r.Category, Is.EqualTo("MyApp.Services.TaskItemService"));
        Assert.That(r.MessageTemplate, Is.EqualTo("Task not created: {OrganizationId}"));
        Assert.That(r.Message, Is.EqualTo("Task not created: 0b7d..."));
        Assert.That(r.TraceId, Is.EqualTo("4bf92f3577b34da6a3ce929d0e0e4736"));
        Assert.That(r.SpanId, Is.EqualTo("00f067aa0ba902b7"));
        Assert.That(r.Version, Is.EqualTo("1.4.0"));
        Assert.That(r.Host, Is.EqualTo("app01"));
        Assert.That(r.Properties["OrganizationId"], Is.EqualTo("0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91"));
        Assert.That(r.Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-06T11:58:59Z")));
    }

    [Test]
    public async Task Exceptions_use_the_outer_message_and_the_method_as_top_frame_unless_a_property_says_otherwise()
    {
        var row = Row("2026-11-06T11:59:00Z", type: "AppExceptions", severity: 3);
        row["Message"] = null;
        row["OuterMessage"] = "Sequence contains no elements";
        row["ExceptionType"] = "System.InvalidOperationException";
        row["Method"] = "MyApp.Application.Services.TaskItemService.Create";
        var withProp = Row("2026-11-06T11:59:01Z", type: "AppExceptions", severity: 3, props: """{"TopFrame":"From.Property.Frame","ExceptionType":"From.Property.Type"}""");
        withProp["Method"] = "Other.Method";
        withProp["ExceptionType"] = "Other.Type";
        _client.Responses.Enqueue([row, withProp]);
        var source = await Start();
        var records = (await source.RunOnceAsync(default)).Batches.Single().Records;
        Assert.That(records[0].Message, Is.EqualTo("Sequence contains no elements"));
        Assert.That(records[0].ExceptionMessage, Is.EqualTo("Sequence contains no elements"));
        Assert.That(records[0].ExceptionType, Is.EqualTo("System.InvalidOperationException"));
        Assert.That(records[0].TopFrame, Is.EqualTo("MyApp.Application.Services.TaskItemService.Create"));
        Assert.That(records[0].Level, Is.EqualTo(RecordLevel.Error));
        Assert.That(records[1].TopFrame, Is.EqualTo("From.Property.Frame"));
        Assert.That(records[1].ExceptionType, Is.EqualTo("From.Property.Type"));
    }

    [Test]
    public async Task The_watermark_is_the_largest_ingestion_time_and_moves_only_when_committed()
    {
        _client.Responses.Enqueue([Row("2026-11-06T11:58:00Z"), Row("2026-11-06T11:59:00Z")]);
        var source = await Start();
        var first = await source.RunOnceAsync(default);
        Assert.That(await _state.GetWatermarkAsync("appinsights", default), Is.Null, "not committed yet");

        await source.RunOnceAsync(default);   // analysis did not finish: same range again
        Assert.That(_client.Calls[1].Query, Does.Contain("datetime(2026-11-06T11:30:00.0000000Z)"));

        await source.CommitAsync(first.Batches.Single(), default);
        Assert.That(await _state.GetWatermarkAsync("appinsights", default), Is.EqualTo("2026-11-06T11:59:00.0000000Z"));
        await source.RunOnceAsync(default);
        Assert.That(_client.Calls[2].Query, Does.Contain("ingestion_time() > datetime(2026-11-06T11:59:00.0000000Z)"));
    }

    [Test]
    public async Task Records_are_ordered_oldest_first_by_ingestion_time()
    {
        _client.Responses.Enqueue([Row("2026-11-06T11:59:00Z", message: "late"), Row("2026-11-06T11:55:00Z", message: "early")]);
        var source = await Start();
        var records = (await source.RunOnceAsync(default)).Batches.Single().Records;
        Assert.That(records.Select(r => r.Message), Is.EqualTo(new[] { "early", "late" }));
    }

    [Test]
    public async Task When_the_row_limit_is_reached_the_next_poll_continues_immediately()
    {
        _client.Responses.Enqueue([Row("2026-11-06T11:50:00Z"), Row("2026-11-06T11:51:00Z"), Row("2026-11-06T11:52:00Z")]);
        _client.Responses.Enqueue([Row("2026-11-06T11:53:00Z")]);
        var source = await Start();
        Assert.That((await source.RunOnceAsync(default)).Delay, Is.EqualTo(TimeSpan.Zero));
        Assert.That((await source.RunOnceAsync(default)).Delay, Is.EqualTo(TimeSpan.FromSeconds(60)));
        Assert.That((await source.RunOnceAsync(default)).Delay, Is.EqualTo(TimeSpan.FromSeconds(60)), "an empty answer waits poll_seconds");
    }

    [Test]
    public async Task No_rows_means_no_batch()
    {
        var source = await Start();
        Assert.That((await source.RunOnceAsync(default)).Batches, Is.Empty);
    }

    [Test]
    public void Settings_are_validated_at_start()
    {
        Assert.ThrowsAsync<SourceConfigurationException>(async () => await Start(Settings(s => s.Remove("app_role_names"))));
        Assert.ThrowsAsync<SourceConfigurationException>(async () => await Start(Settings(s => s["app_role_names"] = new JsonArray("a') or 1==1 //"))));
    }

    // ---- discovery (6.3) ----

    private static JsonObject Discovery(string name = "myapp-role", string value = "observability") => new()
    {
        ["discovery"] = new JsonObject { ["tag_name"] = name, ["tag_value"] = value, ["resource_type"] = "Microsoft.OperationalInsights/workspaces" },
        ["app_role_names"] = new JsonArray("myapp-api"),
    };

    private WorkspaceResolver Resolver(IWorkspaceDiscoveryClient? client, string? subscriptions = null) =>
        new(client, _state, new MonitorOptions { Azure = { SubscriptionIds = subscriptions } }, _time);

    [Test]
    public async Task Discovery_requires_exactly_one_match_and_the_error_names_the_tag()
    {
        foreach (var found in new[] { Array.Empty<string>(), ["a", "b"] })
        {
            var ex = Assert.ThrowsAsync<SourceConfigurationException>(async () =>
                await Resolver(new FakeDiscoveryClient(found)).ResolveAsync("appinsights", Discovery(), default))!;
            Assert.That(ex.Message, Does.Contain("myapp-role=observability"));
            Assert.That(ex.Message, Does.Contain($"found {found.Length}"));
        }
    }

    [Test]
    public async Task One_discovered_workspace_is_used_cached_and_refreshed_every_six_hours()
    {
        var client = new FakeDiscoveryClient("/subscriptions/s/resourceGroups/rg/providers/Microsoft.OperationalInsights/workspaces/w1");
        var resolver = Resolver(client, "sub-1, sub-2");
        var first = await resolver.ResolveAsync("appinsights", Discovery(), default);
        Assert.That(first, Does.EndWith("/workspaces/w1"));
        Assert.That(client.Last!.Value.Subs, Is.EqualTo(new[] { "sub-1", "sub-2" }));
        Assert.That(client.Last!.Value.Tag, Is.EqualTo("myapp-role"));
        Assert.That(client.Last!.Value.Type, Is.EqualTo("Microsoft.OperationalInsights/workspaces"));

        _time.Advance(TimeSpan.FromHours(5));
        await resolver.ResolveAsync("appinsights", Discovery(), default);
        Assert.That(client.Calls, Is.EqualTo(1), "cached for 6 hours");

        _time.Advance(TimeSpan.FromHours(2));
        client.Found[0] = "/subscriptions/s/other/w2";
        Assert.That(await resolver.ResolveAsync("appinsights", Discovery(), default), Does.EndWith("/w2"));
        Assert.That(client.Calls, Is.EqualTo(2));
    }

    [Test]
    public async Task A_failed_refresh_keeps_the_cached_workspace()
    {
        var client = new FakeDiscoveryClient("ws-cached");
        var resolver = Resolver(client);
        await resolver.ResolveAsync("appinsights", Discovery(), default);
        _time.Advance(TimeSpan.FromHours(7));
        var failing = new ThrowingDiscovery();
        Assert.That(await Resolver(failing).ResolveAsync("appinsights", Discovery(), default), Is.EqualTo("ws-cached"));
    }

    [Test]
    public async Task An_explicit_workspace_id_needs_no_discovery()
    {
        var client = new FakeDiscoveryClient();
        Assert.That(await Resolver(client).ResolveAsync("a", new JsonObject { ["workspace_id"] = "explicit" }, default), Is.EqualTo("explicit"));
        Assert.That(client.Calls, Is.Zero);
    }

    [Test]
    public async Task The_source_uses_the_discovered_workspace_for_its_queries()
    {
        var source = await Start(Settings(s => { s.Remove("workspace_id"); s["discovery"] = Discovery()["discovery"]!.DeepClone(); }), Resolver(new FakeDiscoveryClient("discovered-ws")));
        await source.RunOnceAsync(default);
        Assert.That(_client.Calls.Single().Workspace, Is.EqualTo("discovered-ws"));
    }

    // ---- failure behaviour (6.2) ----

    [Test]
    public async Task A_failing_source_keeps_its_checkpoint_backs_off_10s_doubling_to_5_minutes_and_reports_unhealthy()
    {
        var source = await Start();
        _client.Failure = new InvalidOperationException("workspace unreachable");
        var delays = new List<TimeSpan>();
        for (var i = 0; i < 7; i++) delays.Add((await source.RunOnceAsync(default)).Delay);
        Assert.That(delays.Select(d => d.TotalSeconds), Is.EqualTo(new[] { 10.0, 20, 40, 80, 160, 300, 300 }));
        var health = await source.GetHealthAsync(default);
        Assert.That(health.Status, Is.EqualTo(SourceStatus.Unhealthy));
        Assert.That(health.Error, Is.EqualTo("workspace unreachable"));
        Assert.That(await _state.GetWatermarkAsync("appinsights", default), Is.Null);

        _client.Failure = null;
        var ok = await source.RunOnceAsync(default);
        Assert.That(ok.Delay, Is.EqualTo(TimeSpan.FromSeconds(60)), "backoff is reset");
        Assert.That((await source.GetHealthAsync(default)).Status, Is.EqualTo(SourceStatus.Healthy));
    }

    [Test]
    public async Task After_15_minutes_unhealthy_the_source_raises_monitor_source_failing_once()
    {
        var source = await Start();
        _client.Failure = new InvalidOperationException("down");
        var raised = new List<LogRecord>();
        for (var minute = 0; minute <= 20; minute++)
        {
            var result = await source.RunOnceAsync(default);
            raised.AddRange(result.Batches.SelectMany(b => b.Records));
            _time.Advance(TimeSpan.FromMinutes(1));
        }
        var record = raised.Single();
        Assert.That(record.EventName, Is.EqualTo("monitor.source.failing"));
        Assert.That(record.Level, Is.EqualTo(RecordLevel.Error));
        Assert.That(record.SourceId, Is.EqualTo("appinsights"));
        Assert.That(record.LogKey, Is.Null, "a monitor event is not a log key");
        Assert.That(raised.Count, Is.EqualTo(1));
    }
}

internal sealed class ThrowingDiscovery : IWorkspaceDiscoveryClient
{
    public Task<IReadOnlyList<string>> FindWorkspacesAsync(string tagName, string tagValue, string resourceType, IReadOnlyList<string> subscriptionIds, CancellationToken ct) =>
        throw new InvalidOperationException("ARM is down");
}

public class HttpHealthSourceTests
{
    private FakeTime _time = null!;

    [SetUp]
    public void SetUp() => _time = FakeTime.Default();

    private async Task<HttpHealthSource> Start(IHttpClientFactory http, int failures = 3)
    {
        var source = new HttpHealthSource("api", http);
        var settings = new JsonObject { ["base_url"] = "https://localhost:7342/", ["poll_seconds"] = 60, ["failures_before_signal"] = failures };
        await source.StartAsync(new SourceContext("api", "http_health", "prod", settings, "/", new InMemorySourceStateStore(), _time), default);
        return source;
    }

    private static async Task<List<LogRecord>> Poll(HttpHealthSource s) => (await s.RunOnceAsync(default)).Batches.SelectMany(b => b.Records).ToList();

    [Test]
    public async Task A_healthy_api_emits_nothing_and_the_endpoint_is_slash_health()
    {
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"status":"Healthy","checks":[{"name":"sqlserver","status":"Healthy","durationMs":12}]}""") });
        var source = await Start(new FakeHttpFactory(handler));
        Assert.That(await Poll(source), Is.Empty);
        Assert.That(handler.Requests.Single().ToString(), Is.EqualTo("https://localhost:7342/health"));
        Assert.That((await source.GetHealthAsync(default)).Status, Is.EqualTo(SourceStatus.Healthy));
    }

    [Test]
    public async Task Each_check_that_is_not_healthy_emits_monitor_api_unhealthy_even_when_the_status_code_is_503()
    {
        var source = await Start(FakeHttpFactory.Json(HttpStatusCode.ServiceUnavailable,
            """{"status":"Unhealthy","checks":[{"name":"sqlserver","status":"Unhealthy","durationMs":12},{"name":"disk","status":"Degraded","durationMs":1},{"name":"ok","status":"Healthy","durationMs":1}]}"""));
        var records = await Poll(source);
        Assert.That(records, Has.Count.EqualTo(2));
        Assert.That(records.Select(r => r.Properties["Check"]), Is.EqualTo(new[] { "sqlserver", "disk" }));
        Assert.That(records.All(r => r.EventName == "monitor.api.unhealthy" && r.Level == RecordLevel.Error && r.LogKey is null), Is.True);
        Assert.That(records[0].Properties["Status"], Is.EqualTo("Unhealthy"));
    }

    [Test]
    public async Task An_unhealthy_status_without_checks_is_reported_as_the_overall_check()
    {
        var source = await Start(FakeHttpFactory.Json(HttpStatusCode.ServiceUnavailable, """{"status":"Unhealthy","checks":[]}"""));
        Assert.That((await Poll(source)).Single().Properties["Check"], Is.EqualTo("overall"));
    }

    [Test]
    public async Task Unreachable_is_signalled_after_the_configured_number_of_consecutive_failures_and_resets_on_success()
    {
        var up = false;
        var handler = new FakeHttpHandler(_ => up
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"status":"Healthy","checks":[]}""") }
            : throw new HttpRequestException("connection refused"));
        var source = await Start(new FakeHttpFactory(handler));
        Assert.That(await Poll(source), Is.Empty);
        Assert.That(await Poll(source), Is.Empty);
        var third = (await Poll(source)).Single();
        Assert.That(third.EventName, Is.EqualTo("monitor.api.unreachable"));
        Assert.That(third.Level, Is.EqualTo(RecordLevel.Critical));
        Assert.That(third.Properties["Url"], Is.EqualTo("https://localhost:7342/health"));
        Assert.That((await Poll(source)).Single().EventName, Is.EqualTo("monitor.api.unreachable"), "signalled again while it stays down");

        up = true;
        Assert.That(await Poll(source), Is.Empty);
        up = false;
        Assert.That(await Poll(source), Is.Empty, "the counter restarted");
    }

    [Test]
    public async Task A_page_that_is_not_a_health_document_counts_as_a_failure()
    {
        var source = await Start(FakeHttpFactory.Json(HttpStatusCode.OK, "<html>login</html>"), failures: 1);
        Assert.That((await Poll(source)).Single().EventName, Is.EqualTo("monitor.api.unreachable"));
    }

    [Test]
    public void The_base_url_must_be_absolute() =>
        Assert.ThrowsAsync<SourceConfigurationException>(async () =>
            await new HttpHealthSource("api", FakeHttpFactory.Json(HttpStatusCode.OK, "{}")).StartAsync(
                new SourceContext("api", "http_health", "prod", new JsonObject { ["base_url"] = "relative" }, "/", new InMemorySourceStateStore(), _time), default));
}

public class WorldSourceTests
{
    [Test]
    public async Task The_world_source_reads_logs_jsonl_once_oldest_first()
    {
        var source = new WorldSource("world");
        await source.StartAsync(new SourceContext("world", "world", "test", new JsonObject { ["directory"] = FixtureRepo.WorldRoot }, "/", new InMemorySourceStateStore(), FakeTime.Default()), default);
        var records = await source.ReadAllAsync(default);
        Assert.That(records, Has.Count.EqualTo(3));
        Assert.That(records.Select(r => r.Timestamp), Is.Ordered);
        Assert.That(records[0].LogKey, Is.EqualTo("task.create.access_lost"));
        Assert.That(records[2].EventName, Is.EqualTo("platform.request.unhandled"));
        Assert.That(records[2].ExceptionType, Is.EqualTo("System.InvalidOperationException"));
        Assert.That(await source.ReadAllAsync(default), Is.Empty);
    }

    [Test]
    public void The_factory_registry_offers_each_working_source_kind()
    {
        Assert.That(new ISignalSourceFactory[]
        {
            new LogFileSourceFactory(), new WorldSourceFactory(), new HttpHealthSourceFactory(FakeHttpFactory.Json(HttpStatusCode.OK, "{}")),
            new LogAnalyticsSourceFactory(new FakeLogAnalyticsClient(), new FakeWorkspaceResolver()),
        }.Select(f => f.Create("x").Kind), Is.EqualTo(new[] { "log_files", "world", "http_health", "log_analytics" }));
    }
}
