using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using MyApp.Monitor.Hosting;
using MyApp.Monitor.Investigator;
using MyApp.Monitor.Pack;

namespace MyApp.Monitor.Tests;

/// <summary>A model host that follows a script: each generation takes the next scripted answer. Records every request it received.</summary>
public sealed class ScriptedBackend : IInferenceBackend
{
    private readonly Queue<Func<GenerationRequest, string>> _script = [];

    public string Alias { get; set; } = "investigator-1.0.0@cpu";
    public Exception? ProbeFailure { get; set; }
    public Func<GenerationRequest, Task>? Before { get; set; }
    public List<GenerationRequest> Requests { get; } = [];
    public int Probes { get; private set; }

    public ScriptedBackend Say(string text) { _script.Enqueue(_ => text); return this; }
    public ScriptedBackend Say(Func<GenerationRequest, string> answer) { _script.Enqueue(answer); return this; }
    public ScriptedBackend Fail(Exception ex) { _script.Enqueue(_ => throw ex); return this; }

    public Task<HostInfo> ProbeAsync(CancellationToken ct)
    {
        Probes++;
        return ProbeFailure is null ? Task.FromResult(new HostInfo(Alias)) : Task.FromException<HostInfo>(ProbeFailure);
    }

    public async Task<string> GenerateAsync(GenerationRequest request, CancellationToken ct)
    {
        Requests.Add(request with { Messages = request.Messages.ToList() });
        if (Before is not null) await Before(request);
        if (_script.Count == 0) throw new HostException("the script has no more answers");
        return _script.Dequeue()(request);
    }

    public static string Call(string tool, string argumentsJson = "{}") => $$"""<call>{"tool":"{{tool}}","arguments":{{argumentsJson}}}</call>""";

    public static string Report(
        string verdict = "explained", string? abstainReason = null, string category = "dependency_unavailable", string? cause = "The database did not answer for a few minutes.",
        double confidence = 0.8, string[]? evidence = null, string? story = null, string? area = null, string? reproduction = null) =>
        "<report>" + new JsonObject
        {
            ["verdict"] = verdict, ["abstain_reason"] = abstainReason, ["cause_category"] = category, ["cause"] = cause, ["confidence"] = confidence,
            ["evidence_refs"] = new JsonArray((evidence ?? []).Select(e => (JsonNode?)e).ToArray()), ["area"] = area, ["user_story"] = story,
            ["reproduction"] = reproduction is null ? null : JsonNode.Parse(reproduction), ["next_checks"] = new JsonArray("Ask the database owner."),
        }.ToJsonString() + "</report>";
}

public sealed class ScriptedFactory(Dictionary<string, ScriptedBackend> backends) : IInferenceBackendFactory
{
    public List<(string Tier, Uri Endpoint, JsonObject? Kwargs)> Created { get; } = [];

    public IInferenceBackend Create(string tier, Uri endpoint, JsonObject? chatTemplateKwargs)
    {
        Created.Add((tier, endpoint, chatTemplateKwargs));
        return backends.TryGetValue(tier, out var b) ? b : new ScriptedBackend { ProbeFailure = new HostException($"nothing listens at {endpoint}") };
    }
}

/// <summary>The monitor's wiring on a temporary store with the investigator host in front of scripted model hosts and a model lock written for the pack.</summary>
public sealed class Lab : IDisposable
{
    public Rig Rig { get; }
    public TempDir Dir { get; } = new();
    public ScriptedBackend Small { get; } = new();
    public ScriptedBackend Large { get; } = new() { Alias = "investigator-1.0.0@gpu" };
    public ScriptedFactory Factory { get; }
    public MonitorOptions Options { get; }
    public InvestigatorHost Host { get; private set; }
    public long MemoryMb { get; set; } = 64_000;
    public string LockPath { get; }
    public OperationsPack Pack { get; }

    public Lab(OperationsPack? pack = null, Action<MonitorOptions>? configure = null, string? lockText = null, bool writeLock = true, Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>? services = null, string? health = null)
    {
        Pack = pack ?? SharedFixtures.Pack;
        Rig = health is null ? new Rig(Pack, configure: services) : new Rig(Pack, health: health, configure: services);
        LockPath = Path.Combine(Dir.Path, "models", "model.lock");
        if (writeLock) WriteLock(lockText ?? LockText(Pack));
        Options = new MonitorOptions
        {
            ContentRoot = Dir.Path,
            Investigator = { ModelLock = LockPath, Endpoint = "http://small.test:8092", Endpoints = { ["large"] = "http://large.test:8093" } },
        };
        configure?.Invoke(Options);
        Factory = new ScriptedFactory(new Dictionary<string, ScriptedBackend> { ["small"] = Small, ["large"] = Large });
        Host = NewHost();
    }

    public InvestigatorHost NewHost() => Host = new InvestigatorHost(
        Rig.Holder, Rig.Store, Options, Factory, Protocol.Load(), Rig.Time, NullLogger<InvestigatorHost>.Instance, () => MemoryMb);

    public static string LockText(OperationsPack pack, string version = "1.0.0", string? protocolSha = null, string tiers = "both", string? toolRole = null) => new JsonObject
    {
        ["schema_version"] = 1,
        ["models"] = new JsonObject
        {
            ["investigator"] = new JsonObject
            {
                ["model_version"] = version, ["status"] = "trained",
                ["base_model"] = new JsonObject { ["hub_id"] = "org/base", ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false }, ["tool_role"] = toolRole },
                ["protocol"] = new JsonObject { ["path"] = ".github/agents/support.agent.md", ["sha256"] = protocolSha ?? ProtocolBlock.Sha256(pack.ProtocolText) },
                ["exports"] = new JsonArray(new[] { "small", "large" }.Where(t => tiers == "both" || tiers == t).Select(t => (JsonNode?)new JsonObject
                {
                    ["format"] = "gguf", ["variant"] = "Q4_K_M", ["runtime"] = "server", ["tier"] = t,
                    ["context_length"] = t == "small" ? 8192 : 16384, ["memory_mb"] = t == "small" ? 900 : 6000,
                }).ToArray()),
            },
        },
    }.ToJsonString();

    public void WriteLock(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LockPath)!);
        File.WriteAllText(LockPath, text);
    }

    public void WriteLog(params string[] messages)
    {
        var lines = messages.Select((m, i) => new JsonObject
        {
            ["Timestamp"] = $"2026-11-06T11:5{i}:00.000Z", ["Level"] = "Error", ["Category"] = "MyApp.Application.Services.TaskItemService", ["Message"] = m,
            ["TraceId"] = Rig.Trace, ["Environment"] = "Production",
        }.ToJsonString());
        File.WriteAllLines(Path.Combine(Rig.LogDirectory, "myapp-api-20261106.jsonl"), lines);
    }

    /// <summary>An incident of the keyed task.create.unavailable signature, with its rule verdict.</summary>
    public async Task<string> KeyedIncidentAsync()
    {
        await Rig.FeedAsync(Rig.Keyed("task.create.unavailable", MyApp.Monitor.Sources.RecordLevel.Error));
        return (await Rig.AllIncidentsAsync()).Single().Id;
    }

    public async Task InvestigateAsync(string id, string reason = "requested by a test")
    {
        await Host.RefreshAsync(CancellationToken.None);
        await Host.ProcessAsync(new InvestigationRequest(id, reason), CancellationToken.None);
    }

    public async Task<JsonObject> ReportAsync(string id) => (await Rig.Store.GetAsync(id))!.Report;

    public void Dispose()
    {
        Rig.Dispose();
        Dir.Dispose();
    }
}
