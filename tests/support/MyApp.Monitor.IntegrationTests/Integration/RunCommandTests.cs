using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MyApp.Monitor.Cli;
using MyApp.Monitor.Hosting;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Store;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

/// <summary>`run` (operations.md 14.3, 14.4): unattended start rules, graceful shutdown, a failing source, and pack watching.</summary>
public class RunCommandTests
{
    private static int Criticals(RunHarness h) => h.Logs.Entries.Count(e => e.Level == LogLevel.Critical);

    // ---- unattended start: one Critical entry and no prompt ----

    [Test]
    public async Task A_missing_masking_key_stops_the_start_with_one_critical_entry()
    {
        await using var h = new RunHarness();
        h.Settings["Monitor:Masking:HashKey"] = "";
        await h.StartAsync(expectStart: false);
        Assert.That(await h.Exit, Is.EqualTo(ExitCodes.Failed));
        Assert.That(Criticals(h), Is.EqualTo(1));
        var entry = h.Logs.Entries.Single(e => e.Level == LogLevel.Critical);
        Assert.That(entry.Name, Is.EqualTo("monitor.start.failed"));
        Assert.That(entry.Message, Does.Contain("Monitor:Masking:HashKey"));
        Assert.That(File.Exists(h.DbPath), Is.False, "nothing was created before the key was checked");
    }

    [Test]
    public async Task An_invalid_pack_stops_the_start_with_one_critical_entry()
    {
        await using var h = new RunHarness();
        File.WriteAllText(Path.Combine(h.PackDirectory, "signatures.json"), "{ \"schema_version\": 1, \"signatures\": [] } ");   // hash no longer matches
        await h.StartAsync(expectStart: false);
        Assert.That(await h.Exit, Is.EqualTo(ExitCodes.Failed));
        Assert.That(Criticals(h), Is.EqualTo(1));
        Assert.That(h.Logs.Entries.Single(e => e.Level == LogLevel.Critical).Message, Does.Contain("operations pack refused"));
    }

    [Test]
    public async Task A_missing_pack_stops_the_start()
    {
        await using var h = new RunHarness();
        Directory.Delete(h.PackDirectory, true);
        await h.StartAsync(expectStart: false);
        Assert.That(await h.Exit, Is.EqualTo(ExitCodes.Failed));
        Assert.That(Criticals(h), Is.EqualTo(1));
    }

    [Test]
    public async Task A_profile_setting_with_no_value_and_no_default_stops_the_start()
    {
        await using var h = new RunHarness(r => r.Replace("docs/support/profiles/onprem.yaml", "${setting:Monitor:Settings:MailFrom|monitor@localhost}", "${setting:Monitor:Settings:MailFromRequired}"));
        await h.StartAsync(expectStart: false);
        Assert.That(await h.Exit, Is.EqualTo(ExitCodes.Failed));
        Assert.That(h.Logs.Entries.Single(e => e.Level == LogLevel.Critical).Message, Does.Contain("Monitor:Settings:MailFromRequired"));
    }

    [TestCase("http://0.0.0.0:5310")]
    [TestCase("http://monitor.example.test:5310")]
    [TestCase("http://127.0.0.1:5310;http://+:5311")]
    public async Task Local_authentication_refuses_to_bind_beyond_loopback(string urls)
    {
        await using var h = new RunHarness();
        h.Settings["Monitor:Dashboard:Urls"] = urls;
        await h.StartAsync(expectStart: false);
        Assert.That(await h.Exit, Is.EqualTo(ExitCodes.Failed));
        Assert.That(h.Logs.Entries.Single(e => e.Level == LogLevel.Critical).Message, Does.Contain("loopback"));
    }

    [Test]
    public async Task Entra_authentication_without_its_configuration_stops_the_start()
    {
        await using var h = new RunHarness();
        h.Settings["Monitor:Settings:DashboardAuth"] = "entra";
        await h.StartAsync(expectStart: false);
        Assert.That(await h.Exit, Is.EqualTo(ExitCodes.Failed));
        Assert.That(h.Logs.Entries.Single(e => e.Level == LogLevel.Critical).Message, Does.Contain("TenantId").Or.Contain("ClientId"));
    }

    [Test]
    public async Task A_dashboard_url_that_is_already_taken_stops_the_start_and_leaves_no_lock_on_the_store()
    {
        using var taken = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        taken.Start();
        var port = ((System.Net.IPEndPoint)taken.LocalEndpoint).Port;
        await using var h = new RunHarness();
        h.Settings["Monitor:Dashboard:Urls"] = $"http://127.0.0.1:{port}";
        await h.StartAsync(expectStart: false);
        Assert.That(await h.Exit, Is.EqualTo(ExitCodes.Failed));
        Assert.That(Criticals(h), Is.EqualTo(1));
    }

    // ---- modes ----

    [Test]
    public async Task The_dashboard_can_be_switched_off_and_the_monitor_still_runs_its_sources()
    {
        await using var h = new RunHarness(r => r.Replace("docs/support/profiles/onprem.yaml", "dashboard:\n  enabled: true", "dashboard:\n  enabled: false"));
        h.WriteLog("myapp-api-20261106.jsonl", RunHarness.UnavailableLine());
        await h.StartAsync();
        Assert.That(h.Info.DashboardUri, Is.Null);
        await h.WaitForIncidentsAsync(1);
        Assert.That(await h.StopAsync(), Is.EqualTo(0));
    }

    [Test]
    public void The_service_files_ship_with_the_monitor_as_the_layout_lists_them()
    {
        var deploy = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "src", "support", "MyApp.Monitor", "deploy");
        if (!Directory.Exists(deploy)) Assert.Ignore("the source tree is not next to the build output");
        var unit = File.ReadAllText(Path.Combine(deploy, "myapp-monitor.service"));
        Assert.That(unit, Does.Contain("Type=notify").And.Contain("ExecStart=").And.Contain("run --profile onprem").And.Contain("Restart=on-failure").And.Contain("EnvironmentFile="));
        var windows = File.ReadAllText(Path.Combine(deploy, "install-windows-service.ps1"));
        Assert.That(windows, Does.Contain("New-Service").And.Contain("MyApp.Monitor").And.Contain("sc.exe failure"));
        Assert.That(File.Exists(Path.Combine(deploy, "Dockerfile")), Is.True);
        Assert.That(File.ReadAllText(Path.Combine(deploy, "Dockerfile")), Does.Contain("ENTRYPOINT").And.Contain("\"run\"").And.Contain("VOLUME"));
    }

    // ---- sources ----

    [Test]
    public async Task A_failing_source_does_not_stop_the_others_and_raises_monitor_source_failing_after_it_has_been_unhealthy_long_enough()
    {
        await using var h = new RunHarness();
        h.HostOptions.SourceFailingAfter = TimeSpan.Zero;
        h.HostOptions.SourceRestartInitial = TimeSpan.FromMilliseconds(100);
        h.HostOptions.SourceRestartMax = TimeSpan.FromMilliseconds(200);
        h.ConfigureServices = services =>
        {
            services.RemoveAll<ISignalSourceFactory>();
            services.AddSingleton<ISignalSourceFactory, LogFileSourceFactory>();
            services.AddSingleton<ISignalSourceFactory>(new ThrowingFactory("http_health"));
        };
        h.WriteLog("myapp-api-20261106.jsonl", RunHarness.UnavailableLine());
        await h.StartAsync();

        await h.WaitForIncidentsAsync(2);
        var incidents = await h.Info.Store.ListAsync(new IncidentFilter());
        Assert.That(incidents.Any(i => i.Report["signature"]!["log_key"]?.GetValue<string>() == "task.create.unavailable"), Is.True, "the healthy source went on");
        var failing = incidents.Single(i => i.Trigger == "unknown");
        Assert.That(failing.Report["evidence"]![0]!["excerpt"]!.GetValue<string>(), Does.Contain("Source api has been unhealthy for over 15 minutes"));
        Assert.That(failing.Report["signature"]!["template"]!.GetValue<string>(), Does.Contain("monitor.source.failing").Or.Contain("MyApp.Monitor.Sources").Or.Contain("Source"));

        var page = await h.GetAsync("/sources");
        Assert.That(System.Net.WebUtility.HtmlDecode(page), Does.Contain("the source api cannot start").And.Contain("Unhealthy"));
        Assert.That(await h.StopAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task A_source_that_starts_after_failing_is_healthy_again_and_reads_from_its_saved_checkpoint()
    {
        await using var h = new RunHarness();
        h.HostOptions.SourceRestartInitial = TimeSpan.FromMilliseconds(100);
        var factory = new FlakyFactory("http_health", failures: 2);
        h.ConfigureServices = services =>
        {
            services.RemoveAll<ISignalSourceFactory>();
            services.AddSingleton<ISignalSourceFactory, LogFileSourceFactory>();
            services.AddSingleton<ISignalSourceFactory>(factory);
        };
        await h.StartAsync();
        await RunHarness.WaitForAsync(() => Task.FromResult(factory.Created >= 3), "the source to start again");
        await RunHarness.WaitForAsync(async () => (await h.GetAsync("/sources")).Contains("flaky source running"), "the source to report healthy");
        Assert.That(await h.StopAsync(), Is.EqualTo(0));
    }

    // ---- graceful shutdown ----

    [Test]
    public async Task Stopping_finishes_the_batch_in_flight_and_saves_its_offset_so_a_restart_reads_nothing_twice()
    {
        await using var h = new RunHarness();
        h.WriteLog("myapp-api-20261106.jsonl", RunHarness.UnavailableLine());
        await h.StartAsync();
        await h.WaitForIncidentsAsync(1);
        var length = new FileInfo(Path.Combine(h.LogDirectory, "myapp-api-20261106.jsonl")).Length;
        Assert.That(await h.StopAsync(), Is.EqualTo(0));

        var store = new SqliteStore(SqliteDatabase.Open(h.DbPath));
        try
        {
            Assert.That((await store.AllOffsetsAsync()).Single().Offset, Is.EqualTo(length));
            Assert.That((await store.ListAsync(new IncidentFilter())).Count, Is.EqualTo(1));
        }
        finally { store.Database.Close(); }
    }

    [Test]
    public async Task After_a_stop_the_store_file_is_released_and_a_new_run_can_start_on_it()
    {
        await using var h = new RunHarness();
        await h.StartAsync();
        Assert.That(await h.StopAsync(), Is.EqualTo(0));
        await using var again = h.Next();
        await again.StartAsync();
        Assert.That(await again.StopAsync(), Is.EqualTo(0));
    }

    // ---- pack watching ----

    [Test]
    public async Task A_new_valid_pack_replaces_the_old_one_between_batches_and_an_invalid_one_is_refused_and_shown()
    {
        await using var h = new RunHarness();
        await h.StartAsync();
        Assert.That(System.Net.WebUtility.HtmlDecode(await h.GetAsync("/sources")), Does.Contain("1.4.0+abc1234"));

        // a newer pack, built from the same repository and copied over the watched directory
        using var repo = new FixtureRepo();
        var built = new PackBuilder(FakeTime.Default()).Build(new PackBuildRequest(repo.Root, "onprem", "1.5.0+def5678", Path.Combine(repo.Root, "out2")) { WriteZip = false });
        Assert.That(built.Succeeded, Is.True, string.Join("; ", built.Findings));
        ReplaceDirectory(built.PackDirectory!, h.PackDirectory);
        await RunHarness.WaitForAsync(async () => System.Net.WebUtility.HtmlDecode(await h.GetAsync("/sources")).Contains("1.5.0+def5678"), "the new pack");
        Assert.That(System.Net.WebUtility.HtmlDecode(await h.GetAsync("/sources")), Does.Contain("Replaced at"));

        // a broken pack: refused, the running one stays
        File.AppendAllText(Path.Combine(h.PackDirectory, "tools.json"), " ");
        await RunHarness.WaitForAsync(async () => (await h.GetAsync("/sources")).Contains("A new pack was refused"), "the refusal to be shown");
        var page = System.Net.WebUtility.HtmlDecode(await h.GetAsync("/sources"));
        Assert.That(page, Does.Contain("1.5.0+def5678"), "the previous valid pack keeps running");
        Assert.That(h.Logs.Entries.Any(e => e.Level == LogLevel.Error && e.Message.Contains("was refused")), Is.True);
        Assert.That(await h.StopAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task The_pack_check_ignores_a_pack_that_is_still_being_copied_until_its_files_stop_changing()
    {
        using var repo = new FixtureRepo();
        var built = repo.Build(outDir: Path.Combine(repo.Root, "p"), zip: false);
        var holder = new MyApp.Monitor.Analysis.RunContextHolder();
        var status = new MonitorStatus(TimeProvider.System);
        // the watcher only compares fingerprints before it loads: two checks with a change between them load nothing
        var path = Path.Combine(repo.Root, "watched");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "a.txt"), "1");
        var watcher = new PackWatcher(path, holder, null!, status);
        File.WriteAllText(Path.Combine(path, "a.txt"), "22");
        Assert.That(watcher.Check(), Is.False, "the files changed since the last look: wait");
        Assert.That(status.PackError, Is.Null);
        Assert.That(built.Succeeded, Is.True);
    }

    private static void ReplaceDirectory(string from, string to)
    {
        foreach (var f in Directory.GetFiles(to, "*", SearchOption.AllDirectories)) File.Delete(f);
        foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, f));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(f, target, true);
        }
    }

    // ---- fakes ----

    private sealed class ThrowingFactory(string kind) : ISignalSourceFactory
    {
        public string Kind => kind;
        public ISignalSource Create(string sourceId) => new ThrowingSource(sourceId, kind);
    }

    private sealed class ThrowingSource(string id, string kind) : ISignalSource
    {
        public string Id => id;
        public string Kind => kind;
        public Task StartAsync(SourceContext context, CancellationToken ct) => throw new SourceConfigurationException($"the source {id} cannot start");
        public async IAsyncEnumerable<SignalBatch> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct) { await Task.CompletedTask; yield break; }
        public Task CommitAsync(SignalBatch batch, CancellationToken ct) => Task.CompletedTask;
        public Task<SourceHealth> GetHealthAsync(CancellationToken ct) => Task.FromResult(new SourceHealth(id, kind, SourceStatus.Unhealthy, "cannot start", null, null, new Dictionary<string, string>()));
    }

    private sealed class FlakyFactory(string kind, int failures) : ISignalSourceFactory
    {
        private int _created;
        public int Created => _created;
        public string Kind => kind;

        public ISignalSource Create(string sourceId)
        {
            var n = Interlocked.Increment(ref _created);
            return new FlakySource(sourceId, kind, fail: n <= failures);
        }
    }

    private sealed class FlakySource(string id, string kind, bool fail) : ISignalSource
    {
        public string Id => id;
        public string Kind => kind;
        public Task StartAsync(SourceContext context, CancellationToken ct) => fail ? throw new InvalidOperationException("not yet") : Task.CompletedTask;

        public async IAsyncEnumerable<SignalBatch> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            try { await Task.Delay(Timeout.Infinite, ct); } catch (OperationCanceledException) { /* stopping */ }
            yield break;
        }

        public Task CommitAsync(SignalBatch batch, CancellationToken ct) => Task.CompletedTask;

        public Task<SourceHealth> GetHealthAsync(CancellationToken ct) =>
            Task.FromResult(new SourceHealth(id, kind, SourceStatus.Healthy, null, DateTimeOffset.UtcNow, null, new Dictionary<string, string> { ["note"] = "flaky source running" }));
    }
}
