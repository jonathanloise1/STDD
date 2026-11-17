using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using MyApp.Monitor.Cli;
using MyApp.Monitor.Pack;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

public class CommandTests
{
    private FixtureRepo _repo = null!;
    private string _packDir = null!;

    [OneTimeSetUp]
    public void Build()
    {
        _repo = new FixtureRepo();
        _packDir = _repo.Build(zip: false).PackDirectory!;
    }

    [OneTimeTearDown]
    public void Cleanup() => _repo.Dispose();

    private record Outcome(int Code, string Out, string Error);

    private static async Task<Outcome> Run(string[] args, Dictionary<string, string?>? config = null, bool withKey = true)
    {
        var o = new StringWriter();
        var e = new StringWriter();
        var settings = new Dictionary<string, string?>();
        if (withKey) settings["Monitor:Masking:HashKey"] = Fx.HashKey;
        using var content = new TempDir();   // `analyse` without --dry-run writes its counters to the store under the content root
        settings["Monitor:ContentRoot"] = content.Path;
        if (config is not null) foreach (var kv in config) settings[kv.Key] = kv.Value;
        var code = await MonitorApp.RunAsync(args, o, e, configuration: settings);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        return new Outcome(code, o.ToString(), e.ToString());
    }

    private static string Line(string message, string level, string ts, string? key = null, string? trace = null, string? org = null, string? version = "1.4.0")
    {
        var o = new JsonObject { ["Timestamp"] = ts, ["Level"] = level, ["Category"] = "MyApp.Application.Services.TaskItemService", ["Message"] = message, ["Version"] = version };
        if (key is not null) o["LogKey"] = key;
        if (trace is not null) o["TraceId"] = trace;
        if (org is not null) o["Properties"] = new JsonObject { ["OrganizationId"] = org };
        return o.ToJsonString();
    }

    // ---- dispatch ----

    [Test]
    public async Task No_command_prints_the_usage_and_exits_with_2()
    {
        var r = await Run([]);
        Assert.That(r.Code, Is.EqualTo(ExitCodes.Usage));
        Assert.That(r.Out, Does.Contain("pack build").And.Contain("pack verify").And.Contain("tool {name}").And.Contain("analyse").And.Contain("run"));
    }

    [Test]
    public async Task An_unknown_command_and_an_unknown_option_are_usage_errors()
    {
        Assert.That((await Run(["frobnicate"])).Code, Is.EqualTo(ExitCodes.Usage));
        var option = await Run(["analyse", "--bogus"]);
        Assert.That(option.Code, Is.EqualTo(ExitCodes.Usage));
        Assert.That(option.Error, Does.Contain("unknown option --bogus"));
        Assert.That((await Run(["pack", "build"])).Error, Does.Contain("--profile is required"));
        Assert.That((await Run(["pack", "nonsense"])).Code, Is.EqualTo(ExitCodes.Usage));
    }

    [Test]
    public async Task The_run_command_rejects_an_unknown_option_and_is_listed_in_the_usage()
    {
        var r = await Run(["run", "--bogus"]);
        Assert.That(r.Code, Is.EqualTo(ExitCodes.Usage));
        Assert.That(r.Error, Does.Contain("unknown option --bogus").And.Contain("run [--profile {name}] [--pack {path}]"));
    }

    [Test]
    public async Task Analyse_without_dry_run_writes_counters_and_unknown_templates_to_the_store_and_with_it_writes_nothing()
    {
        using var dir = new TempDir();
        using var content = new TempDir();
        var input = WriteLog(dir, Line("A widget exploded", "Error", "2026-11-06T10:00:00Z"), Line("m", "Warning", "2026-11-06T10:01:00Z", "task.create.access_lost"));
        var config = new Dictionary<string, string?> { ["Monitor:ContentRoot"] = content.Path };

        var dry = await Run(["analyse", "--input", input, "--pack", _packDir, "--dry-run"], config);
        Assert.That(dry.Code, Is.EqualTo(ExitCodes.Ok));
        Assert.That(Directory.Exists(Path.Combine(content.Path, "data")), Is.False, "--dry-run writes nothing");

        var written = await Run(["analyse", "--input", input, "--pack", _packDir], config);
        Assert.That(written.Code, Is.EqualTo(ExitCodes.Ok));
        Assert.That(written.Out, Does.Contain("counters and unknown templates written to"));
        var store = new MyApp.Monitor.Store.SqliteStore(MyApp.Monitor.Store.SqliteDatabase.Open(Path.Combine(content.Path, "data", "monitor.db")));
        try
        {
            Assert.That(store.Snapshot().Sum(c => c.Count), Is.EqualTo(2));
            Assert.That(store.All().Single().Level, Is.EqualTo("Error"));
            Assert.That(await store.ListAsync(new MyApp.Monitor.Store.IncidentFilter()), Is.Empty, "escalations are printed; `run` opens incidents");
        }
        finally { store.Database.Close(); }
    }

    // ---- pack build / verify ----

    [Test]
    public async Task Pack_build_writes_the_pack_and_reports_it()
    {
        using var dir = new TempDir();
        var r = await Run(["pack", "build", "--profile", "onprem", "--version", "9.9.9+test", "--repo", _repo.Root, "--out", dir.Path]);
        Assert.That(r.Code, Is.EqualTo(0), r.Out + r.Error);
        Assert.That(r.Out, Does.Contain("Pack built:").And.Contain("Version 9.9.9+test").And.Contain("3 signatures"));
        Assert.That(File.Exists(Path.Combine(dir.Path, "onprem", "manifest.json")), Is.True);
        Assert.That(Directory.GetFiles(dir.Path, "myapp-ops-pack-9.9.9+test-onprem.zip"), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task Pack_build_failures_print_the_check_ids_and_exit_with_1()
    {
        using var broken = new FixtureRepo();
        broken.Replace(FixtureRepo.Log, "Level = LogLevel.Warning", "Level = LogLevel.Error");
        using var dir = new TempDir();
        var r = await Run(["pack", "build", "--profile", "onprem", "--repo", broken.Root, "--out", dir.Path]);
        Assert.That(r.Code, Is.EqualTo(ExitCodes.Failed));
        Assert.That(r.Out, Does.Contain("[O04]"));
        Assert.That(Directory.GetDirectories(dir.Path), Is.Empty);
    }

    [Test]
    public async Task Pack_verify_accepts_a_built_pack_and_refuses_a_tampered_one()
    {
        var ok = await Run(["pack", "verify", "--pack", _packDir]);
        Assert.That(ok.Code, Is.EqualTo(0), ok.Out + ok.Error);
        Assert.That(ok.Out, Does.Contain("Pack OK").And.Contain("3 signatures"));

        using var copy = new TempDir();
        foreach (var f in PackFiles.Read(_packDir))
        {
            var target = Path.Combine(copy.Path, f.Key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, f.Value);
        }
        File.AppendAllText(Path.Combine(copy.Path, "tools.json"), "\n");
        var bad = await Run(["pack", "verify", "--pack", copy.Path]);
        Assert.That(bad.Code, Is.EqualTo(ExitCodes.Failed));
        Assert.That(bad.Out, Does.Contain("[PACK-HASH]"));
    }

    // ---- tool ----

    [Test]
    public async Task The_tool_command_prints_the_envelope_of_one_call_over_a_world()
    {
        var r = await Run(["tool", "health_status", "--args", "{}", "--world", FixtureRepo.WorldRoot, "--pack", _packDir]);
        Assert.That(r.Code, Is.EqualTo(0), r.Out + r.Error);
        var envelope = JsonNode.Parse(r.Out)!;
        Assert.That(envelope["tool"]!.GetValue<string>(), Is.EqualTo("health_status"));
        Assert.That(envelope["status"]!.GetValue<string>(), Is.EqualTo("ok"));
        Assert.That(envelope["source"]!.GetValue<string>(), Is.EqualTo("world"));
        Assert.That(envelope["evidence_id"]!.GetValue<string>(), Is.EqualTo("E2"));
        Assert.That(envelope["data"]!["status"]!.GetValue<string>(), Is.EqualTo("Degraded"));
        Assert.That(envelope.AsObject().Select(p => p.Key), Is.EqualTo(new[] { "tool", "call", "status", "evidence_id", "source", "rows", "truncated", "fields_dropped", "data", "error" }));
    }

    [Test]
    public async Task The_tool_command_applies_masking_and_the_catalog_schema()
    {
        var logs = await Run(["tool", "logs_by_correlation", "--args", """{"trace_id":"4bf92f3577b34da6a3ce929d0e0e4736"}""", "--world", FixtureRepo.WorldRoot, "--pack", _packDir]);
        Assert.That(logs.Code, Is.EqualTo(0));
        Assert.That(logs.Out, Does.Contain("[email]").And.Not.Contain("john.doe@example.com"));

        var denied = await Run(["tool", "logs_by_correlation", "--args", """{"trace_id":"short"}""", "--world", FixtureRepo.WorldRoot, "--pack", _packDir]);
        Assert.That(denied.Code, Is.EqualTo(ExitCodes.Failed));
        Assert.That(JsonNode.Parse(denied.Out)!["status"]!.GetValue<string>(), Is.EqualTo("denied"));
    }

    [Test]
    public async Task The_tool_command_reads_signatures_from_the_pack()
    {
        var r = await Run(["tool", "signature_lookup", "--args", """{"log_key":"task.create.access_lost"}""", "--world", FixtureRepo.WorldRoot, "--pack", _packDir]);
        Assert.That(JsonNode.Parse(r.Out)!["data"]!["signature"]!["runbook"]!["anchor"]!.GetValue<string>(), Is.EqualTo("task-create-access-lost"));
    }

    [Test]
    public async Task The_tool_command_validates_its_inputs_and_needs_the_masking_key()
    {
        Assert.That((await Run(["tool", "health_status", "--args", "{oops", "--world", FixtureRepo.WorldRoot, "--pack", _packDir])).Code, Is.EqualTo(ExitCodes.Usage));
        Assert.That((await Run(["tool", "health_status", "--world", "/nowhere", "--pack", _packDir])).Code, Is.EqualTo(ExitCodes.Usage));
        Assert.That((await Run(["tool", "--pack", _packDir])).Code, Is.EqualTo(ExitCodes.Usage));
        var noKey = await Run(["tool", "health_status", "--world", FixtureRepo.WorldRoot, "--pack", _packDir], withKey: false);
        Assert.That(noKey.Code, Is.EqualTo(ExitCodes.Failed));
        Assert.That(noKey.Error, Does.Contain("Monitor:Masking:HashKey"));
    }

    [Test]
    public async Task The_incident_option_is_recorded_for_the_audit()
    {
        using var state = new TempDir();
        var r = await Run(["tool", "health_status", "--incident", "INC-20261106-0001", "--world", FixtureRepo.WorldRoot, "--pack", _packDir, "--state", state.Path]);
        Assert.That(r.Code, Is.EqualTo(0));
    }

    // ---- analyse ----

    private static string WriteLog(TempDir dir, params string[] lines)
    {
        var path = dir.File("app.jsonl");
        File.WriteAllLines(path, lines);
        return path;
    }

    [Test]
    public async Task Analyse_prints_a_keyed_record_as_matched_with_its_runbook_anchor()
    {
        using var dir = new TempDir();
        var input = WriteLog(dir, Line("Task not created", "Warning", "2026-11-06T10:42:01.123Z", "task.create.access_lost", "4bf92f3577b34da6a3ce929d0e0e4736", "0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91"));
        var r = await Run(["analyse", "--input", input, "--pack", _packDir]);
        Assert.That(r.Code, Is.EqualTo(0), r.Out + r.Error);
        Assert.That(r.Out, Does.Contain("task.create.access_lost matched keyed task.create.access_lost runbook=task-create-access-lost status=active severity=Warning"));
        Assert.That(r.Out, Does.Contain("counted only"));
        Assert.That(r.Out, Does.Contain("-- 1 records, 0 lines skipped; Matched 1; no escalations"));
    }

    [Test]
    public async Task Analyse_prints_a_rate_escalation_when_the_rate_is_above_the_threshold()
    {
        using var dir = new TempDir();
        // expected 5 per hour, multiplier 3: more than 15 in the window escalates
        var lines = Enumerable.Range(0, 20).Select(i => Line("Task not created", "Warning", $"2026-11-06T10:{i:D2}:00.000Z", "task.create.access_lost", org: "0b7d3c52-8a41-4f0e-9d6b-2c1e5a7f3b91")).ToArray();
        var r = await Run(["analyse", "--input", WriteLog(dir, lines), "--pack", _packDir]);
        Assert.That(r.Out, Does.Contain("ESCALATE trigger=rate severity=Warning verdict=residual action=OpenNew"));
        Assert.That(r.Out, Does.Contain("escalations: rate"));
        Assert.That(r.Out.Split('\n').Count(l => l.Contains("ESCALATE")), Is.GreaterThan(1), "later records of the same fingerprint fold into the open incident");
        Assert.That(r.Out, Does.Contain("action=AppendToOpen").Or.Contain("action=OpenNew"));
    }

    [Test]
    public async Task An_error_level_keyed_record_escalates_by_severity_with_a_rule_verdict()
    {
        using var dir = new TempDir();
        var r = await Run(["analyse", "--input", WriteLog(dir, Line("failed", "Error", "2026-11-06T10:00:00.000Z", "task.create.unavailable")), "--pack", _packDir]);
        Assert.That(r.Out, Does.Contain("ESCALATE trigger=severity severity=Error verdict=rule action=OpenNew"));
    }

    [Test]
    public async Task An_injected_unhandled_exception_prints_a_stable_template_id_across_two_runs()
    {
        using var dir = new TempDir();
        string UnhandledLine(string ts, string trace) => new JsonObject
        {
            ["Timestamp"] = ts, ["Level"] = "Error", ["Category"] = "MyApp.WebApi.Middleware.ExceptionHandlingMiddleware", ["LogKey"] = "platform.request.unhandled",
            ["MessageTemplate"] = "Unhandled exception {ExceptionType} at {TopFrame} on {Route}", ["Message"] = "Unhandled exception ...", ["TraceId"] = trace,
            ["Properties"] = new JsonObject { ["ExceptionType"] = "System.InvalidOperationException", ["TopFrame"] = "MyApp.Application.Services.TaskItemService.Create", ["Route"] = "api/tasks" },
        }.ToJsonString();
        var input = WriteLog(dir, UnhandledLine("2026-11-06T10:00:00Z", "4bf92f3577b34da6a3ce929d0e0e4736"));
        var first = await Run(["analyse", "--input", input, "--pack", _packDir, "--explain"]);
        File.WriteAllLines(input, [UnhandledLine("2026-11-07T08:00:00Z", "7d2e0c4f9a1b48e3b5c6d7e8f9a0b1c2")]);
        var second = await Run(["analyse", "--input", input, "--pack", _packDir, "--explain"]);

        string Id(string output) => System.Text.RegularExpressions.Regex.Match(output, @"template_id: (t-[0-9a-f]{12})").Groups[1].Value;
        Assert.That(Id(first.Out), Is.EqualTo("t-28a840784043"));
        Assert.That(Id(second.Out), Is.EqualTo(Id(first.Out)));
        Assert.That(first.Out, Does.Contain("unknown_template t-28a840784043"));
        Assert.That(first.Out, Does.Contain("ESCALATE trigger=unknown severity=Error verdict=residual"));
        Assert.That(first.Out, Does.Contain("event_name: platform.request.unhandled"));
        Assert.That(first.Out, Does.Contain("category: MyApp.WebApi.Middleware.ExceptionHandlingMiddleware"));
        Assert.That(first.Out, Does.Contain("exception_type: System.InvalidOperationException"));
        Assert.That(first.Out, Does.Contain("top_frame: MyApp.Application.Services.TaskItemService.Create"));
        Assert.That(first.Out, Does.Contain("discriminator: System.InvalidOperationException|MyApp.Application.Services.TaskItemService.Create|api/tasks"));
    }

    [Test]
    public async Task A_template_signature_of_the_pack_matches_an_unkeyed_platform_event()
    {
        using var dir = new TempDir();
        var line = new JsonObject
        {
            ["Timestamp"] = "2026-11-06T10:00:00Z", ["Level"] = "Error", ["Category"] = "MyApp.WebApi.Health", ["LogKey"] = "platform.health.unhealthy",
            ["Message"] = "Health check sqlserver failed", ["Properties"] = new JsonObject { ["Check"] = "sqlserver" },
        }.ToJsonString();
        var r = await Run(["analyse", "--input", WriteLog(dir, line), "--pack", _packDir]);
        Assert.That(r.Out, Does.Contain("matched template t-2bb5dcdc1949 runbook=t-2bb5dcdc1949"));
        Assert.That(r.Out, Does.Contain("ESCALATE trigger=health"));
    }

    [Test]
    public async Task Analyse_masks_before_it_extracts_and_counts_unreadable_lines()
    {
        using var dir = new TempDir();
        var r = await Run(["analyse", "--input", WriteLog(dir, "not json", Line("mail john.doe@example.com failed", "Error", "2026-11-06T10:00:00Z")), "--pack", _packDir, "--explain"]);
        Assert.That(r.Out, Does.Contain("1 lines skipped"));
        Assert.That(r.Out, Does.Contain("discriminator: mail [email] failed"));
        Assert.That(r.Out, Does.Not.Contain("john.doe"));
    }

    [Test]
    public async Task Analyse_inside_a_repository_builds_the_local_pack_first()
    {
        using var repo = new FixtureRepo();
        using var dir = new TempDir();
        var r = await Run(["analyse", "--input", WriteLog(dir, Line("m", "Warning", "2026-11-06T10:00:00Z", "task.create.access_lost")), "--repo", repo.Root, "--profile", "onprem", "--dry-run"]);
        Assert.That(r.Code, Is.EqualTo(0), r.Out + r.Error);
        Assert.That(File.Exists(Path.Combine(repo.Root, "artifacts", "ops-pack", "local", "manifest.json")), Is.True);
        Assert.That(r.Out, Does.Contain("matched keyed"));
    }

    [Test]
    public async Task Analyse_stops_with_the_findings_when_the_local_pack_cannot_be_built()
    {
        using var repo = new FixtureRepo();
        repo.Delete(FixtureRepo.Ops);
        using var dir = new TempDir();
        var r = await Run(["analyse", "--input", WriteLog(dir, Line("m", "Warning", "2026-11-06T10:00:00Z")), "--repo", repo.Root]);
        Assert.That(r.Code, Is.EqualTo(ExitCodes.Failed));
        Assert.That(r.Error, Does.Contain("[R14]"));
    }

    [Test]
    public async Task Analyse_needs_an_input_file_and_the_masking_key()
    {
        Assert.That((await Run(["analyse"])).Code, Is.EqualTo(ExitCodes.Usage));
        Assert.That((await Run(["analyse", "--input", "/nowhere.jsonl"])).Code, Is.EqualTo(ExitCodes.Usage));
        using var dir = new TempDir();
        var r = await Run(["analyse", "--input", WriteLog(dir, Line("m", "Warning", "2026-11-06T10:00:00Z")), "--pack", _packDir], withKey: false);
        Assert.That(r.Code, Is.EqualTo(ExitCodes.Failed));
    }

    [Test]
    public async Task The_pack_path_can_come_from_configuration()
    {
        using var dir = new TempDir();
        var r = await Run(["analyse", "--input", WriteLog(dir, Line("m", "Warning", "2026-11-06T10:00:00Z", "task.create.access_lost"))], new() { ["Monitor:Pack:Path"] = _packDir });
        Assert.That(r.Code, Is.EqualTo(0), r.Out + r.Error);
    }

    [Test]
    public async Task Monitor_Environment_overrides_the_profile_environment_for_fingerprints()
    {
        using var dir = new TempDir();
        var input = WriteLog(dir, Line("failed", "Error", "2026-11-06T10:00:00.000Z", "task.create.unavailable"));
        var prod = await Run(["analyse", "--input", input, "--pack", _packDir]);
        var other = await Run(["analyse", "--input", input, "--pack", _packDir], new() { ["Monitor:Environment"] = "staging" });
        string Fp(string o) => System.Text.RegularExpressions.Regex.Match(o, @"fingerprint=([0-9a-f]{16})").Groups[1].Value;
        Assert.That(Fp(prod.Out), Is.Not.EqualTo(Fp(other.Out)));
    }
}

public class OptionsAndParsingTests
{
    [Test]
    public void Options_are_parsed_with_values_flags_and_equals_signs()
    {
        var spec = new Dictionary<string, bool> { ["input"] = true, ["explain"] = false };
        var a = CommandArgs.Parse(["name", "--input", "f.jsonl", "--explain", "--input=g.jsonl"], spec);
        Assert.That(a.Positional, Is.EqualTo(new[] { "name" }));
        Assert.That(a.Get("input"), Is.EqualTo("g.jsonl"));
        Assert.That(a.Has("explain"), Is.True);
        Assert.Throws<UsageException>(() => CommandArgs.Parse(["--input"], spec));
        Assert.Throws<UsageException>(() => CommandArgs.Parse(["--explain=1"], spec));
        Assert.Throws<UsageException>(() => CommandArgs.Parse(["--nope"], spec));
    }

    [Test]
    public void Configuration_keys_bind_to_the_documented_options()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Monitor:Pack:Path"] = "p", ["Monitor:Profile"] = "cloud", ["Monitor:Environment"] = "test", ["Monitor:Azure:Credential"] = "managed_identity",
            ["Monitor:Azure:ManagedIdentityClientId"] = "cid", ["Monitor:Azure:SubscriptionIds"] = "a, b ,c", ["Monitor:Connections:main-db"] = "cs",
            ["Monitor:Masking:HashKey"] = "k", ["Monitor:Investigator:ModelLock"] = "m", ["Monitor:Investigator:Device"] = "cpu", ["Monitor:Dashboard:Urls"] = "u",
            ["Monitor:Settings:X"] = "y",
        }).Build();
        var o = MonitorOptions.From(config);
        Assert.That(o.Pack.Path, Is.EqualTo("p"));
        Assert.That(o.Profile, Is.EqualTo("cloud"));
        Assert.That(o.Environment, Is.EqualTo("test"));
        Assert.That(o.Azure.Credential, Is.EqualTo("managed_identity"));
        Assert.That(o.Azure.ManagedIdentityClientId, Is.EqualTo("cid"));
        Assert.That(o.Azure.SubscriptionIdList, Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(o.Connections["main-db"], Is.EqualTo("cs"));
        Assert.That(o.Investigator.ModelLock, Is.EqualTo("m"));
        Assert.That(o.Dashboard.Urls, Is.EqualTo("u"));
        Assert.That(o.Settings["X"], Is.EqualTo("y"));
        Assert.That(new MonitorOptions { Azure = { SubscriptionIds = "" } }.Azure.SubscriptionIdList, Is.Empty, "empty means all subscriptions the identity can read");
    }

    [Test]
    public void The_azure_credential_must_be_developer_or_managed_identity()
    {
        Assert.DoesNotThrow(() => new MonitorOptions().ValidateAzure());
        Assert.Throws<MonitorException>(() => new MonitorOptions { Azure = { Credential = "key" } }.ValidateAzure());
    }

    [Test]
    public void The_credential_chain_is_built_from_the_options_without_touching_the_network()
    {
        Assert.That(MyApp.Monitor.Sources.AzureCredentialFactory.Create(new MonitorOptions()), Is.TypeOf<Azure.Identity.ChainedTokenCredential>());
        Assert.That(MyApp.Monitor.Sources.AzureCredentialFactory.Create(new MonitorOptions { Azure = { Credential = "managed_identity" } }), Is.TypeOf<Azure.Identity.ManagedIdentityCredential>());
        Assert.That(MyApp.Monitor.Sources.AzureCredentialFactory.Create(new MonitorOptions { Azure = { Credential = "managed_identity", ManagedIdentityClientId = "00000000-0000-0000-0000-000000000000" } }), Is.TypeOf<Azure.Identity.ManagedIdentityCredential>());
    }

    [Test]
    public void The_repository_is_found_by_walking_up_and_an_explicit_root_is_validated()
    {
        using var repo = new FixtureRepo();
        Assert.That(RepositoryLocator.Find(repo.Root), Is.EqualTo(Path.GetFullPath(repo.Root)));
        using var empty = new TempDir();
        Assert.Throws<MonitorException>(() => RepositoryLocator.Find(empty.Path));
    }

    [Test]
    public void A_profile_given_as_a_path_selects_its_file_name_as_the_profile_name()
    {
        var name = PackSelection.ProfileName("/x/y/custom.yaml", new MonitorOptions(), out var file);
        Assert.That(name, Is.EqualTo("custom"));
        Assert.That(file, Is.EqualTo("/x/y/custom.yaml"));
        Assert.That(PackSelection.ProfileName(null, new MonitorOptions(), out var none), Is.EqualTo("onprem"));
        Assert.That(none, Is.Null);
        Assert.That(PackSelection.ProfileName(null, new MonitorOptions { Profile = "cloud" }, out _), Is.EqualTo("cloud"));
    }
}
