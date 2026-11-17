using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyApp.Application.Help;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

/// <summary>Pack loading and the start-up checks of models.md 7.5; the application must start with nothing present.</summary>
[TestFixture]
[Property("UserStory", "US-HELP-05")]
public class HelpRuntimeTests
{
    private HelpFiles _files = null!;
    private CapturingLogger<HelpRuntime> _log = null!;

    [SetUp]
    public void SetUp()
    {
        _files = new HelpFiles();
        _log = new CapturingLogger<HelpRuntime>();
    }

    [TearDown]
    public void TearDown() => _files.Dispose();

    private HelpRuntime Load(Action<HelpSettingsBuilder>? change = null) =>
        HelpRuntime.Load(_files.Settings(change), Path.GetTempPath(), _log);

    [Test]
    public void NoPackAndNoLock_StartsWithRetrievalOnlyAndLogsTheReasonOnce()
    {
        var runtime = HelpRuntime.Load(new HelpSettings(), Path.GetTempPath(), _log);

        Assert.That(runtime.Compatibility.Compatible, Is.False);
        Assert.That(runtime.Compatibility.Reason, Is.EqualTo(IncompatibleReasons.NoPack));
        Assert.That(runtime.Packs, Is.Empty);
        Assert.That(runtime.Lock, Is.Null);
        Assert.That(runtime.GenerationAllowed, Is.False);
        Assert.That(_log.Entries, Has.Count.EqualTo(1));
        Assert.That(_log.Entries[0].Level, Is.EqualTo(LogLevel.Information));
        Assert.That(_log.Entries[0].Message, Does.Contain("no_pack").And.Contain("retrieval only"));
    }

    [Test]
    public void ADirectoryWithoutAManifest_IsNoPack()
    {
        var runtime = Load();

        Assert.That(runtime.Compatibility.Reason, Is.EqualTo(IncompatibleReasons.NoPack));
        Assert.That(runtime.Packs, Is.Empty);
    }

    [Test]
    public void PackAndLockThatAgree_AreCompatible()
    {
        var sha = _files.WritePack("en");
        _files.WritePack("it");
        _files.WriteLock("1.0.0");

        var runtime = Load();

        Assert.That(runtime.Compatibility, Is.EqualTo(new AssistantCompatibility(true, null, null)));
        Assert.That(runtime.GenerationAllowed, Is.True);
        Assert.That(runtime.Packs.Keys, Is.EquivalentTo(new[] { "en", "it" }));
        Assert.That(runtime.Packs["en"].Sha256, Is.EqualTo(sha));
        Assert.That(runtime.Packs["en"].ProjectName, Is.EqualTo("TaskFlow"));
        Assert.That(runtime.Packs["en"].Sections, Has.Count.EqualTo(2));
        Assert.That(runtime.Lock!.ModelVersion, Is.EqualTo("1.0.0"));
        Assert.That((bool)runtime.Lock.ChatTemplateKwargs!["enable_thinking"]!, Is.False);
        Assert.That(_log.Entries, Has.Count.EqualTo(1));
        Assert.That(_log.Entries[0].Message, Does.Contain("generation tiers are eligible"));
    }

    [Test]
    public void PackWithoutLock_TurnsGenerationOff()
    {
        _files.WritePack("en");

        var runtime = Load();

        Assert.That(runtime.Compatibility.Reason, Is.EqualTo(IncompatibleReasons.NoLock));
        Assert.That(runtime.Packs, Has.Count.EqualTo(1), "retrieval keeps working");
        Assert.That(_log.Entries.Single().Message, Does.Contain("no_lock"));
    }

    [Test]
    public void LockThatCannotBeParsed_TurnsGenerationOff()
    {
        _files.WritePack("en");
        File.WriteAllText(_files.LockPath, "{ not json");

        Assert.That(Load().Compatibility.Reason, Is.EqualTo(IncompatibleReasons.NoLock));
    }

    [Test]
    public void LockWithoutAssistantEntry_TurnsGenerationOff()
    {
        _files.WritePack("en");
        File.WriteAllText(_files.LockPath, "{\"schema_version\":1,\"models\":{}}");

        Assert.That(Load().Compatibility.Reason, Is.EqualTo(IncompatibleReasons.NoLock));
    }

    [Test]
    public void ProtocolOfThePackThatDiffersFromTheLock_TurnsGenerationOff()
    {
        _files.WritePack("en");
        _files.WriteLock(protocolSha: new string('0', 64));

        var runtime = Load();

        Assert.That(runtime.Compatibility.Compatible, Is.False);
        Assert.That(runtime.Compatibility.Reason, Is.EqualTo(IncompatibleReasons.ProtocolMismatch));
        Assert.That(_log.Entries.Single().Message, Does.Contain("protocol_mismatch"));
    }

    [Test]
    public void PackThatSaysNotCompatible_TurnsGenerationOff()
    {
        _files.WritePack("en", compatible: false);
        _files.WriteLock();

        Assert.That(Load().Compatibility.Reason, Is.EqualTo(IncompatibleReasons.PackNotCompatible));
    }

    [Test]
    public void ThePackOfOneLanguageThatDisagrees_TurnsGenerationOffForAll()
    {
        _files.WritePack("en");
        _files.WritePack("it", protocolSha: new string('1', 64));
        _files.WriteLock();

        var runtime = Load();

        Assert.That(runtime.Compatibility.Reason, Is.EqualTo(IncompatibleReasons.ProtocolMismatch));
        Assert.That(runtime.Compatibility.Detail, Does.Contain("'it'"));
    }

    [Test]
    public void PackThatDoesNotMatchTheManifestHash_IsNotLoaded()
    {
        _files.WritePack("en", manifestSha: new string('a', 64));
        _files.WriteLock();

        var runtime = Load();

        Assert.That(runtime.Packs, Is.Empty);
        Assert.That(runtime.Compatibility.Reason, Is.EqualTo(IncompatibleReasons.PackUnreadable));
    }

    [Test]
    public void PackOfHelpPack2WithAnEmbeddingsBlock_AndALockOfSchema2WithAnEmbeddingsEntry_AreAcceptedUnchanged()
    {
        var sha = _files.WritePack("en", schemaVersion: 2, embeddings: true);
        _files.WritePack("it", schemaVersion: 2);
        _files.WriteLock(schemaVersion: 2, embeddingsEntry: true);

        var runtime = Load();

        Assert.That(HelpRuntime.KnownSchemaVersion, Is.EqualTo(2));
        Assert.That(runtime.Compatibility, Is.EqualTo(new AssistantCompatibility(true, null, null)));
        Assert.That(runtime.Packs.Keys, Is.EquivalentTo(new[] { "en", "it" }));
        Assert.That(runtime.Packs["en"].Sha256, Is.EqualTo(sha));
        Assert.That(runtime.Packs["en"].Sections, Has.Count.EqualTo(2));
        Assert.That(runtime.Lock!.ModelVersion, Is.EqualTo("1.0.0"), "the assistant entry is read, the embeddings entry ignored");
    }

    [Test]
    public void LockOfSchema1_IsStillRead()
    {
        _files.WritePack("en", schemaVersion: 2);
        _files.WriteLock(schemaVersion: 1);

        Assert.That(Load().Compatibility.Compatible, Is.True);
    }

    [Test]
    public void LockWithANewerSchemaVersion_TurnsGenerationOff()
    {
        _files.WritePack("en", schemaVersion: 2);
        _files.WriteLock(schemaVersion: HelpRuntime.KnownSchemaVersion + 1);

        Assert.That(Load().Compatibility.Reason, Is.EqualTo(IncompatibleReasons.NoLock));
    }

    [Test]
    public void PackWithANewerSchemaVersion_IsRefused()
    {
        _files.WritePack("en", schemaVersion: HelpRuntime.KnownSchemaVersion + 1);
        _files.WriteLock();

        var runtime = Load();

        Assert.That(runtime.Packs, Is.Empty);
        Assert.That(runtime.Compatibility.Reason, Is.EqualTo(IncompatibleReasons.PackUnreadable));
    }

    [Test]
    public void ManifestThatCannotBeParsed_IsUnreadable()
    {
        File.WriteAllText(Path.Combine(_files.PackDirectory, "manifest.json"), "][");

        Assert.That(Load().Compatibility.Reason, Is.EqualTo(IncompatibleReasons.PackUnreadable));
    }

    [Test]
    public void APackFileOutsideTheDirectory_IsNotRead()
    {
        _files.WritePack("en");
        var manifestPath = Path.Combine(_files.PackDirectory, "manifest.json");
        File.WriteAllText(manifestPath, File.ReadAllText(manifestPath).Replace("en-pack.json", "../en-pack.json"));

        Assert.That(Load().Packs, Is.Empty);
    }

    [Test]
    public void PackWithoutProtocolContent_SearchesButCannotGenerate()
    {
        _files.WritePack("en", protocol: false);
        _files.WriteLock();

        var runtime = Load();

        Assert.That(runtime.Packs, Has.Count.EqualTo(1));
        Assert.That(runtime.Compatibility.Reason, Is.EqualTo(IncompatibleReasons.PackNotCompatible));
    }

    [Test]
    public void RelativePaths_AreResolvedFromTheContentRoot()
    {
        _files.WritePack("en");
        _files.WriteLock();
        var root = Path.GetDirectoryName(_files.PackDirectory)!;

        var runtime = HelpRuntime.Load(new HelpSettings { PackDirectory = "help", LockPath = "model.lock" }, root, _log);

        Assert.That(runtime.Compatibility.Compatible, Is.True);
    }

    [Test]
    public void Settings_HaveTheDocumentedDefaults()
    {
        var settings = HelpSettings.Load(new ConfigurationBuilder().Build());

        Assert.That(settings.GapsEnabled, Is.True);
        Assert.That(settings.GapsRetentionDays, Is.EqualTo(90));
        Assert.That(settings.Order, Is.EqualTo("browser-first"));
        Assert.That(settings.BrowserEnabled, Is.True);
        Assert.That(settings.BrowserModelBaseUrl, Is.EqualTo("/models/assistant/"));
        Assert.That(settings.BrowserMinTokensPerSecond, Is.EqualTo(8));
        Assert.That(settings.BrowserAskBeforeDownloadMb, Is.EqualTo(100));
        Assert.That(settings.BrowserTimeoutSeconds, Is.EqualTo(20));
        Assert.That(settings.ServerEnabled, Is.False);
        Assert.That(settings.ServerEndpoint, Is.EqualTo("http://127.0.0.1:8091"));
        Assert.That(settings.ServerTimeoutSeconds, Is.EqualTo(20));
        Assert.That(settings.ServerMaxConcurrent, Is.EqualTo(2));
        Assert.That(settings.ServerQueueTimeoutSeconds, Is.EqualTo(5));
    }

    [Test]
    public void Settings_ReadTheKeysAndIgnoreMalformedValues()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MyApp:Assistant:Order"] = "Server-First",
            ["MyApp:Assistant:Server:Enabled"] = "true",
            ["MyApp:Assistant:Server:MaxConcurrent"] = "4",
            ["MyApp:Assistant:Server:TimeoutSeconds"] = "abc",
            ["MyApp:Help:Gaps:Enabled"] = "false",
            ["MyApp:Help:Gaps:RetentionDays"] = "0"
        }).Build();

        var settings = HelpSettings.Load(configuration);

        Assert.That(settings.Order, Is.EqualTo("server-first"));
        Assert.That(settings.ServerEnabled, Is.True);
        Assert.That(settings.ServerMaxConcurrent, Is.EqualTo(4));
        Assert.That(settings.ServerTimeoutSeconds, Is.EqualTo(20));
        Assert.That(settings.GapsEnabled, Is.False);
        Assert.That(settings.GapsRetentionDays, Is.EqualTo(90));
    }

    [Test]
    public void TheShippedAppSettings_CarryEveryKeyOfModelsSevenSevenWithItsDefault()
    {
        foreach (var file in new[] { "appsettings.json", "appsettings.Development.json.example" })
        {
            var path = Path.Combine(HelpFiles.RepoRoot(), "src", "backend", "MyApp.WebApi", file);
            var configuration = new ConfigurationBuilder().AddJsonFile(path).Build();

            foreach (var key in new[]
                     {
                         "MyApp:Help:PackDirectory", "MyApp:Help:Gaps:Enabled", "MyApp:Help:Gaps:RetentionDays", "MyApp:Models:LockPath",
                         "MyApp:Assistant:Order", "MyApp:Assistant:Browser:Enabled", "MyApp:Assistant:Browser:ModelBaseUrl",
                         "MyApp:Assistant:Browser:MinTokensPerSecond", "MyApp:Assistant:Browser:AskBeforeDownloadMb",
                         "MyApp:Assistant:Browser:TimeoutSeconds", "MyApp:Assistant:Server:Enabled", "MyApp:Assistant:Server:Endpoint",
                         "MyApp:Assistant:Server:TimeoutSeconds", "MyApp:Assistant:Server:MaxConcurrent", "MyApp:Assistant:Server:QueueTimeoutSeconds"
                     })
                Assert.That(configuration[key], Is.Not.Null, $"{file}: {key}");

            var settings = HelpSettings.Load(configuration);
            Assert.That(settings.GapsRetentionDays, Is.EqualTo(90), file);
            Assert.That(settings.Order, Is.EqualTo("browser-first"), file);
            Assert.That(settings.ServerEnabled, Is.False, file);
            Assert.That(settings.ServerEndpoint, Is.EqualTo("http://127.0.0.1:8091"), file);
            Assert.That(settings.ServerMaxConcurrent, Is.EqualTo(2), file);
            Assert.That(settings.ServerQueueTimeoutSeconds, Is.EqualTo(5), file);
        }
    }

    [Test]
    public void TheDevelopmentExample_PointsAtThePackAndTheLockOfTheRepository()
    {
        var path = Path.Combine(HelpFiles.RepoRoot(), "src", "backend", "MyApp.WebApi", "appsettings.Development.json.example");
        var settings = HelpSettings.Load(new ConfigurationBuilder().AddJsonFile(path).Build());
        var contentRoot = Path.Combine(HelpFiles.RepoRoot(), "src", "backend", "MyApp.WebApi");

        Assert.That(Path.GetFullPath(Path.Combine(contentRoot, settings.PackDirectory)),
            Is.EqualTo(Path.Combine(HelpFiles.RepoRoot(), "src", "frontend", "public", "help")));
        Assert.That(Path.GetFullPath(Path.Combine(contentRoot, settings.LockPath)),
            Is.EqualTo(Path.Combine(HelpFiles.RepoRoot(), "models", "model.lock")));
    }
}
