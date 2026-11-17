using System.Security.Cryptography;
using System.Text;
using MyApp.Application.Help;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using Newtonsoft.Json.Linq;

namespace MyApp.UnitTests.Backend;

/// <summary>A pack directory and a lock file written to a temporary folder, removed on dispose.</summary>
internal sealed class HelpFiles : IDisposable
{
    public const string SectionA = "tasks/US-TASK-01#steps";
    public const string SectionB = "tasks/US-TASK-01#overview";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "myapp-help-" + Guid.NewGuid().ToString("N"));

    public HelpFiles()
    {
        Directory.CreateDirectory(PackDirectory);
    }

    public string PackDirectory => Path.Combine(_root, "help");
    public string LockPath => Path.Combine(_root, "model.lock");

    /// <summary>The real protocol of the template: the bytes, the SHA-256 of the file and the parsed content.</summary>
    public static (string Sha256, JObject Content) Protocol()
    {
        var path = Path.Combine(RepoRoot(), "training", "assistant", "protocol.json");
        var bytes = File.ReadAllBytes(path);
        return (Convert.ToHexStringLower(SHA256.HashData(bytes)), JObject.Parse(Encoding.UTF8.GetString(bytes)));
    }

    public static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "training", "assistant", "protocol.json")))
                return dir.FullName;
        }
        throw new InvalidOperationException("The repository root was not found.");
    }

    /// <summary>Writes the pack of one language and returns its file SHA-256 (the <c>pack_sha256</c>).</summary>
    public string WritePack(string lang = "en", bool compatible = true, string? protocolSha = null, bool protocol = true, int schemaVersion = 1, bool writeManifest = true,
        string? manifestSha = null, bool embeddings = false)
    {
        var (sha, content) = Protocol();
        var pack = new JObject
        {
            ["schema_version"] = schemaVersion,
            ["kind"] = "help-pack",
            ["lang"] = lang,
            ["project"] = new JObject { ["name"] = "TaskFlow", ["version"] = "0.1.0" },
            ["assistant"] = new JObject
            {
                ["protocol"] = protocol ? new JObject { ["sha256"] = protocolSha ?? sha, ["content"] = content } : new JObject { ["sha256"] = protocolSha ?? sha },
                ["model"] = new JObject { ["id"] = "assistant", ["version"] = "1.0.0" },
                ["compatible"] = compatible
            },
            ["sections"] = new JArray(
                Section(SectionA, "Create a task - Steps", "1. Open the Tasks page.\n2. Click \"New Task\".\n3. Click \"Create Task\"."),
                Section(SectionB, "Create a task", "Tasks are created from the Tasks page.")),
            // help-pack 2 (manual.md 6.7.4): the block that names the embedding table; the backend never loads the table
            ["embeddings"] = embeddings
                ? new JObject
                {
                    ["file"] = "embed.bin", ["sha256"] = new string('c', 64), ["bytes"] = 2247, ["format_version"] = 1, ["dims"] = 128, ["count"] = 100,
                    ["model"] = new JObject { ["id"] = "embeddings", ["version"] = "1.0.0", ["hub_id"] = "Qwen/Qwen3-Embedding-0.6B", ["revision"] = new string('a', 40) },
                    ["locked"] = true, ["vocab_coverage"] = 0.97, ["calibrated"] = true,
                    ["params"] = new JObject { ["method"] = "rrf", ["depth"] = 30, ["rrf_k"] = 60, ["embedding_weight"] = 1.0, ["min_cosine"] = 0.55, ["min_cosine_lexical"] = -1.0 }
                }
                : JValue.CreateNull()
        };
        var bytes = Encoding.UTF8.GetBytes(pack.ToString(Newtonsoft.Json.Formatting.None));
        var fileSha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        File.WriteAllBytes(Path.Combine(PackDirectory, lang + "-pack.json"), bytes);

        if (writeManifest)
        {
            var manifestPath = Path.Combine(PackDirectory, "manifest.json");
            var manifest = File.Exists(manifestPath)
                ? JObject.Parse(File.ReadAllText(manifestPath))
                : new JObject { ["schema_version"] = schemaVersion, ["packs"] = new JObject() };
            ((JObject)manifest["packs"]!)[lang] = new JObject { ["file"] = lang + "-pack.json", ["sha256"] = manifestSha ?? fileSha };
            if (schemaVersion >= 2)
            {
                manifest["embeddings"] ??= new JObject();
                ((JObject)manifest["embeddings"]!)[lang] = embeddings ? new JObject { ["file"] = lang + "/embed.bin", ["sha256"] = new string('c', 64) } : JValue.CreateNull();
            }
            File.WriteAllText(manifestPath, manifest.ToString());
        }
        return fileSha;
    }

    private static JObject Section(string id, string title, string text) => new() { ["id"] = id, ["title"] = title, ["text"] = text };

    public void WriteLock(string version = "1.0.0", string? protocolSha = null, JObject? kwargs = null, int schemaVersion = 1, bool embeddingsEntry = false)
    {
        var (sha, _) = Protocol();
        var entry = new JObject
        {
            ["model_version"] = version,
            ["status"] = "trained",
            ["base_model"] = new JObject { ["hub_id"] = "x/y", ["chat_template_kwargs"] = kwargs ?? new JObject { ["enable_thinking"] = false } },
            ["protocol"] = new JObject { ["path"] = "training/assistant/protocol.json", ["sha256"] = protocolSha ?? sha }
        };
        var models = new JObject { ["assistant"] = entry };
        if (embeddingsEntry)   // models.md 8.1: its own keys, no protocol; the assistant's reading is unaffected
            models["embeddings"] = new JObject
            {
                ["model_version"] = "1.0.0", ["status"] = "distilled",
                ["base_model"] = new JObject { ["hub_id"] = "Qwen/Qwen3-Embedding-0.6B", ["revision"] = new string('a', 40) },
                ["card"] = JValue.CreateNull()
            };
        File.WriteAllText(LockPath, new JObject
        {
            ["schema_version"] = schemaVersion,
            ["models"] = models
        }.ToString());
    }

    public HelpSettings Settings(Action<HelpSettingsBuilder>? change = null)
    {
        var b = new HelpSettingsBuilder { PackDirectory = PackDirectory, LockPath = LockPath };
        change?.Invoke(b);
        return b.Build();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}

/// <summary>Mutable view of <see cref="HelpSettings"/> for the tests.</summary>
internal sealed class HelpSettingsBuilder
{
    public string PackDirectory { get; set; } = "";
    public string LockPath { get; set; } = "";
    public string Order { get; set; } = HelpSettings.OrderBrowserFirst;
    public bool BrowserEnabled { get; set; } = true;
    public bool ServerEnabled { get; set; } = true;
    public int ServerTimeoutSeconds { get; set; } = 20;
    public int ServerMaxConcurrent { get; set; } = 2;
    public int ServerQueueTimeoutSeconds { get; set; } = 5;
    public bool GapsEnabled { get; set; } = true;

    public HelpSettings Build() => new()
    {
        PackDirectory = PackDirectory,
        LockPath = LockPath,
        Order = Order,
        BrowserEnabled = BrowserEnabled,
        ServerEnabled = ServerEnabled,
        ServerTimeoutSeconds = ServerTimeoutSeconds,
        ServerMaxConcurrent = ServerMaxConcurrent,
        ServerQueueTimeoutSeconds = ServerQueueTimeoutSeconds,
        GapsEnabled = GapsEnabled
    };
}

/// <summary>A model host that answers from a script.</summary>
internal sealed class FakeModelHost : IModelHost
{
    public ModelHostInfo? Info { get; set; } = new("assistant-1.0.0@cpu");
    public string[] Deltas { get; set; } = [];
    public Exception? StartFailure { get; set; }

    /// <summary>The host never accepts the request (until the caller cancels).</summary>
    public bool HangOnStart { get; set; }

    /// <summary>After the deltas the host never finishes (until the caller cancels).</summary>
    public bool HangAfterDeltas { get; set; }

    public ModelChatRequest? LastRequest { get; private set; }
    public int Starts { get; private set; }

    public Task<ModelHostInfo?> GetInfoAsync(CancellationToken cancellationToken) => Task.FromResult(Info);

    public async Task<IAsyncEnumerable<string>> StartAsync(ModelChatRequest request, CancellationToken cancellationToken)
    {
        Starts++;
        LastRequest = request;
        if (StartFailure is not null)
            throw StartFailure;
        if (HangOnStart)
            await Task.Delay(Timeout.Infinite, cancellationToken);
        return Stream();
    }

    private async IAsyncEnumerable<string> Stream([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var delta in Deltas)
            yield return delta;
        if (HangAfterDeltas)
            await Task.Delay(Timeout.Infinite, ct);
    }
}

/// <summary>Documentation gaps held in memory.</summary>
internal sealed class FakeHelpGapsRepository : IHelpGapsRepository
{
    public List<HelpGap> Items { get; } = [];

    public Task AddAsync(HelpGap gap, CancellationToken cancellationToken = default)
    {
        Items.Add(gap);
        return Task.CompletedTask;
    }

    public Task<List<HelpGap>> ListAsync(DateTime? from, DateTime? toExclusive, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(g => (from is null || g.RecordedAt >= from) && (toExclusive is null || g.RecordedAt < toExclusive))
            .OrderBy(g => g.RecordedAt).ToList());

    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.RemoveAll(g => g.RecordedAt < cutoffUtc));
}
