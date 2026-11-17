using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using MyApp.Monitor.Investigator;
using NUnit.Framework;

namespace MyApp.Monitor.Tests.Integration;

/// <summary>The tiers and the selection of operations.md 11.2, the compatibility check against the model lock, and the HTTP client against a fake llama-server.</summary>
public class InvestigatorSelectionTests
{
    private Lab _lab = null!;

    [SetUp]
    public void SetUp() => _lab = new Lab();

    [TearDown]
    public void TearDown() => _lab.Dispose();

    private Task<InvestigatorSelection> Select(string tier = "small", string device = "auto", string? lockPath = "default", string? protocolSha = null)
    {
        var selector = new InvestigatorSelector(_lab.Factory, _lab.Rig.Time, () => _lab.MemoryMb);
        return selector.SelectAsync(new SelectionInput(tier, device, lockPath == "default" ? _lab.LockPath : lockPath, protocolSha ?? ProtocolBlock.Sha256(_lab.Pack.ProtocolText), _lab.Options.Investigator), default);
    }

    [Test]
    public async Task Tier_none_is_rules_only_without_asking_any_host()
    {
        var s = await Select("none");
        Assert.That((s.Status.State, s.IsReady, _lab.Factory.Created), Is.EqualTo((InvestigatorStates.Off, false, new List<(string, Uri, JsonObject?)>())));
    }

    [Test]
    public async Task A_missing_lock_is_rules_only_with_the_reason()
    {
        var s = await Select(lockPath: Path.Combine(_lab.Dir.Path, "nowhere", "model.lock"));
        Assert.That(s.IsReady, Is.False);
        Assert.That(s.Status.Reason, Does.Contain("model lock was not found").And.Contain("rules only"));
        Assert.That((await Select(lockPath: null)).Status.Reason, Does.Contain("Monitor:Investigator:ModelLock"));
    }

    [Test]
    public async Task A_lock_made_for_another_protocol_is_refused()
    {
        var s = await Select(protocolSha: new string('0', 64));
        Assert.That(s.IsReady, Is.False);
        Assert.That(s.Status.Reason, Does.Contain("protocol").And.Contain("rules only"));
    }

    [Test]
    public async Task The_small_tier_uses_the_host_and_reports_its_device_and_the_lock_kwargs_reach_the_backend()
    {
        var s = await Select();
        Assert.That((s.Status.State, s.Status.Tier, s.Status.Device, s.Status.ModelVersion), Is.EqualTo((InvestigatorStates.Ready, "small", "cpu", "1.0.0")));
        Assert.That(s.Export, Is.EqualTo(new TierExport("small", 8192, 900)));
        var created = _lab.Factory.Created.Single();
        Assert.That((created.Tier, created.Endpoint.ToString()), Is.EqualTo(("small", "http://small.test:8092/")));
        Assert.That(created.Kwargs!.ToJsonString(), Is.EqualTo("""{"enable_thinking":false}"""));
    }

    [Test]
    public async Task The_large_tier_uses_its_own_host_and_falls_back_to_small_when_nothing_answers_there_or_memory_is_short()
    {
        var large = await Select("large");
        Assert.That((large.Status.Tier, large.Status.Device), Is.EqualTo(("large", "gpu")));
        Assert.That(large.Export!.ContextLength, Is.EqualTo(16384));

        _lab.Large.ProbeFailure = new HostException("refused");
        var fallback = await Select("large");
        Assert.That((fallback.Status.Tier, fallback.IsReady), Is.EqualTo(("small", true)));
        Assert.That(fallback.Status.Reason, Does.Contain("fell back from large"));

        _lab.Large.ProbeFailure = null;
        _lab.Large.Alias = "investigator-1.0.0@cpu";
        _lab.MemoryMb = 4000;                                  // the large export needs 6000 on the CPU, the small one 900
        var tight = await Select("large");
        Assert.That(tight.Status.Tier, Is.EqualTo("small"));

        _lab.MemoryMb = 500;
        var none = await Select("large");
        Assert.That(none.IsReady, Is.False);
        Assert.That(none.Status.Reason, Does.Contain("more than this machine has"));
    }

    [Test]
    public async Task A_forced_device_is_refused_when_the_host_runs_elsewhere()
    {
        Assert.That((await Select(device: "gpu")).IsReady, Is.False);
        Assert.That((await Select(device: "cpu")).Status.Device, Is.EqualTo("cpu"));
        Assert.That((await Select("large", "cpu")).Status.Tier, Is.EqualTo("small"), "the large host is on the GPU: the profile wants the CPU, the small host is");
        var s = await Select(device: "gpu");
        Assert.That(s.Status.Reason, Does.Contain("requires the gpu"));
    }

    [Test]
    public async Task A_host_serving_another_model_version_or_no_host_at_all_is_rules_only()
    {
        _lab.Small.Alias = "investigator-0.9.0@cpu";
        Assert.That((await Select()).Status.Reason, Does.Contain("investigator-0.9.0@cpu").And.Contain("investigator-1.0.0"));
        _lab.Small.Alias = "assistant-1.0.0@cpu";
        Assert.That((await Select()).IsReady, Is.False);
        _lab.Small.Alias = "investigator-1.0.0";
        Assert.That((await Select()).Status.Reason, Does.Contain("does not say gpu or cpu"));
        _lab.Small.ProbeFailure = new HostException("no host answers at http://small.test:8092/");
        var s = await Select();
        Assert.That((s.IsReady, s.Status.State), Is.EqualTo((false, InvestigatorStates.RulesOnly)));
        Assert.That(s.Status.Reason, Does.Contain("no host answers"));
    }

    [Test]
    public async Task A_tier_the_lock_does_not_have_is_skipped()
    {
        _lab.WriteLock(Lab.LockText(_lab.Pack, tiers: "small"));
        var s = await Select("large");
        Assert.That(s.Status.Tier, Is.EqualTo("small"));
    }

    [Test]
    public async Task The_host_check_runs_at_refresh_and_the_status_names_the_tier_and_device()
    {
        await _lab.Host.RefreshAsync(default);
        Assert.That(_lab.Host.IsAvailable, Is.True);
        Assert.That((_lab.Host.Status.Tier, _lab.Host.Status.Device, _lab.Host.Status.State), Is.EqualTo(("small", "cpu", InvestigatorStates.Ready)));
        _lab.Small.ProbeFailure = new HostException("gone");
        await _lab.Host.RefreshAsync(default);
        Assert.That(_lab.Host.IsAvailable, Is.False, "a host that stops answering is rules only until it answers again");
        _lab.Small.ProbeFailure = null;
        await _lab.Host.RefreshAsync(default);
        Assert.That(_lab.Host.IsAvailable, Is.True);
    }

    [Test]
    public async Task The_tool_role_of_the_lock_reaches_the_selection_and_defaults_to_tool()
    {
        Assert.That((await Select()).Export!.ToolRole, Is.EqualTo("tool"), "a lock without tool_role keeps the protocol's role");
        _lab.WriteLock(Lab.LockText(_lab.Pack, toolRole: "user"));
        Assert.That((await Select()).Export!.ToolRole, Is.EqualTo("user"));
        _lab.WriteLock(Lab.LockText(_lab.Pack, toolRole: "tool"));
        Assert.That((await Select()).Export!.ToolRole, Is.EqualTo("tool"));
        _lab.WriteLock(Lab.LockText(_lab.Pack, toolRole: "something-else"));
        Assert.That((await Select()).Export!.ToolRole, Is.EqualTo("tool"), "only user changes the role, as in the pipeline");
    }

    [Test]
    public async Task The_tool_role_of_a_tier_entry_wins_over_the_top_level_base_model()
    {
        var lockJson = (JsonObject)JsonNode.Parse(Lab.LockText(_lab.Pack, toolRole: "user"))!;
        var entry = (JsonObject)lockJson["models"]!["investigator"]!;
        entry["tiers"] = new JsonObject { ["small"] = new JsonObject { ["base_model"] = new JsonObject { ["tool_role"] = "tool" } } };
        _lab.WriteLock(lockJson.ToJsonString());
        Assert.That((await Select("small")).Export!.ToolRole, Is.EqualTo("tool"));
        Assert.That((await Select("large")).Export!.ToolRole, Is.EqualTo("user"));
    }

    [Test]
    public async Task The_request_to_the_host_carries_no_tools_field_and_no_native_tool_tags()
    {
        using var server = new FakeLlamaServer("investigator-1.0.0@gpu", "<report>{}</report>");
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var backend = new LlamaServerBackend(http, server.Uri, new JsonObject { ["enable_thinking"] = false });
        await backend.GenerateAsync(new GenerationRequest([new ChatMessage("system", "rules"), new ChatMessage("user", "<result>{}</result>")], 700, null), default);
        var body = (JsonObject)JsonNode.Parse(server.LastBody!)!;
        Assert.That(body.ContainsKey("tools") || body.ContainsKey("tool_choice"), Is.False);
        Assert.That(body["messages"]!.AsArray().SelectMany(m => ((JsonObject)m!).Select(kv => kv.Key)).Distinct(), Is.EquivalentTo(new[] { "role", "content" }));
    }

    [Test]
    public void The_investigator_sources_never_name_the_native_tool_format_or_pass_tools_to_the_template()
    {
        var dir = Path.Combine(Repo.Root, "src", "support", "MyApp.Monitor", "Investigator");
        foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
        {
            var text = File.ReadAllText(file);
            foreach (var tag in new[] { "<tool_call>", "</tool_call>", "<function=", "<parameter=", "<tool_response>", "</tool_response>" })
                Assert.That(text, Does.Not.Contain(tag), $"{Path.GetFileName(file)} names the native tag {tag}");
        }
        Assert.That(File.ReadAllText(Path.Combine(dir, "Backend.cs")), Does.Not.Contain("[\"tools\"]"), "the request body has no tools field");
    }

    // ---- the HTTP client against a fake llama-server ----

    [Test]
    public async Task The_llama_server_client_probes_the_alias_and_asks_for_a_greedy_grammar_constrained_turn()
    {
        using var server = new FakeLlamaServer("investigator-1.0.0@gpu", "<call>{\"tool\":\"health_status\",\"arguments\":{}}</call>");
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var backend = new LlamaServerBackend(http, server.Uri, new JsonObject { ["enable_thinking"] = false });

        var info = await backend.ProbeAsync(default);
        Assert.That((info.ModelAndVersion, info.Device), Is.EqualTo(("investigator-1.0.0", "gpu")));

        var answer = await backend.GenerateAsync(new GenerationRequest([new ChatMessage("system", "rules"), new ChatMessage("user", "hello")], 700, "root ::= \"x\""), default);
        Assert.That(answer, Is.EqualTo("<call>{\"tool\":\"health_status\",\"arguments\":{}}</call>"));
        var body = JsonNode.Parse(server.LastBody!)!;
        Assert.That(body["temperature"]!.GetValue<int>(), Is.EqualTo(0));
        Assert.That(body["top_k"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(body["max_tokens"]!.GetValue<int>(), Is.EqualTo(700));
        Assert.That(body["stream"]!.GetValue<bool>(), Is.False);
        Assert.That(body["grammar"]!.GetValue<string>(), Is.EqualTo("root ::= \"x\""));
        Assert.That(body["chat_template_kwargs"]!["enable_thinking"]!.GetValue<bool>(), Is.False);
        Assert.That(body["messages"]!.AsArray().Select(m => m!["role"]!.GetValue<string>()), Is.EqualTo(new[] { "system", "user" }));
    }

    [Test]
    public async Task The_llama_server_client_reports_a_host_that_is_down_not_ready_or_failing_as_a_host_error()
    {
        using var http = new HttpClient();
        var closed = new Uri("http://127.0.0.1:1/");
        Assert.ThrowsAsync<HostException>(() => new LlamaServerBackend(http, closed, null).ProbeAsync(default));
        using var server = new FakeLlamaServer("investigator-1.0.0@cpu", "x") { HealthStatus = 503 };
        var ex = Assert.ThrowsAsync<HostException>(() => new LlamaServerBackend(http, server.Uri, null).ProbeAsync(default));
        Assert.That(ex!.Message, Does.Contain("not ready"));
        server.HealthStatus = 200;
        server.ChatStatus = 500;
        var chat = Assert.ThrowsAsync<HostException>(() => new LlamaServerBackend(http, server.Uri, null).GenerateAsync(new GenerationRequest([new ChatMessage("user", "x")], 10, null), default));
        Assert.That(chat!.Message, Does.Contain("500"));
    }
}

/// <summary>Just enough of llama-server: <c>/health</c>, <c>/v1/models</c> and <c>/v1/chat/completions</c> on a loopback port.</summary>
public sealed class FakeLlamaServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly string _alias;
    private readonly string _answer;
    private readonly Task _loop;

    public Uri Uri { get; }
    public int HealthStatus { get; set; } = 200;
    public int ChatStatus { get; set; } = 200;
    public string? LastBody { get; private set; }

    public FakeLlamaServer(string alias, string answer)
    {
        _alias = alias;
        _answer = answer;
        var port = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        port.Start();
        var number = ((IPEndPoint)port.LocalEndpoint).Port;
        port.Stop();
        Uri = new Uri($"http://127.0.0.1:{number}/");
        _listener.Prefixes.Add(Uri.ToString());
        _listener.Start();
        _loop = Task.Run(Serve);
    }

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext c;
            try { c = await _listener.GetContextAsync(); } catch (Exception) { return; }
            string text; var status = 200;
            switch (c.Request.Url!.AbsolutePath)
            {
                case "/health": status = HealthStatus; text = "{\"status\":\"ok\"}"; break;
                case "/v1/models": text = new JsonObject { ["data"] = new JsonArray(new JsonObject { ["id"] = _alias }) }.ToJsonString(); break;
                case "/v1/chat/completions":
                    LastBody = await new StreamReader(c.Request.InputStream).ReadToEndAsync();
                    status = ChatStatus;
                    text = status == 200 ? new JsonObject { ["choices"] = new JsonArray(new JsonObject { ["message"] = new JsonObject { ["content"] = _answer } }) }.ToJsonString() : "{\"error\":\"boom\"}";
                    break;
                default: status = 404; text = "{}"; break;
            }
            var bytes = Encoding.UTF8.GetBytes(text);
            c.Response.StatusCode = status;
            c.Response.ContentType = "application/json";
            await c.Response.OutputStream.WriteAsync(bytes);
            c.Response.Close();
        }
    }

    public void Dispose()
    {
        _listener.Close();
        try { _loop.Wait(1000); } catch (Exception) { /* closed */ }
    }
}
