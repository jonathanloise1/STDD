using MyApp.Application.DTOs.Help;
using MyApp.Application.Help;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

/// <summary>The status payload and the answer of the server tier (models.md 7.4, 7.5).</summary>
[TestFixture]
[Property("UserStory", "US-HELP-05")]
public class HelpAssistantTests
{
    private HelpFiles _files = null!;
    private FakeModelHost _host = null!;
    private string _packSha = null!;

    [SetUp]
    public void SetUp()
    {
        _files = new HelpFiles();
        _packSha = _files.WritePack("en");
        _files.WritePack("it");
        _files.WriteLock("1.0.0");
        _host = new FakeModelHost();
    }

    [TearDown]
    public void TearDown() => _files.Dispose();

    private HelpAssistant Create(Action<HelpSettingsBuilder>? change = null)
    {
        var runtime = HelpRuntime.Load(_files.Settings(change), Path.GetTempPath(), new CapturingLogger<HelpRuntime>());
        return new HelpAssistant(runtime, _host, TimeSpan.Zero);
    }

    private HelpAnswerRequest Request(string question = "how do i make a task") => new()
    {
        Lang = "en",
        Question = question,
        SectionIds = [HelpFiles.SectionA, HelpFiles.SectionB],
        PackSha256 = _packSha
    };

    private static async Task<List<HelpAnswerEvent>> Run(HelpAnswerStream stream)
    {
        await using (stream)
            return await stream.Events().ToListAsync();
    }

    private static async Task<HelpAnswerException> Fails(Task<HelpAnswerStream> start)
    {
        var e = Assert.ThrowsAsync<HelpAnswerException>(async () => await start);
        await Task.CompletedTask;
        return e!;
    }

    // ---------------------------------------------------------------- status

    [Test]
    public async Task Status_DefaultsWithAPackAndALock_ListsBothTiersAndTheProbe()
    {
        var status = await Create(b => b.ServerEnabled = false).GetStatusAsync(default);

        Assert.That(status.Order, Is.EqualTo("browser-first"));
        Assert.That(status.Compatible, Is.True);
        Assert.That(status.Reason, Is.Null);
        Assert.That(status.Pack.Loaded, Is.True);
        Assert.That(status.Pack.ProjectVersion, Is.EqualTo("0.1.0"));
        Assert.That(status.Pack.Sha256["en"], Is.EqualTo(_packSha));
        Assert.That(status.Model!.Version, Is.EqualTo("1.0.0"));
        Assert.That(status.Browser.Enabled, Is.True);
        Assert.That(status.Browser.ModelBaseUrl, Is.EqualTo("/models/assistant/"));
        Assert.That(status.Browser.MinTokensPerSecond, Is.EqualTo(8));
        Assert.That(status.Browser.AskBeforeDownloadMb, Is.EqualTo(100));
        Assert.That(status.Browser.TimeoutSeconds, Is.EqualTo(20));
        Assert.That(status.Server.Enabled, Is.False);
        Assert.That(status.Server.Available, Is.False);
        Assert.That(status.GapsEnabled, Is.True);
    }

    [Test]
    public async Task Status_ServerEnabledAndHostServingTheLockedVersion_ReportsTheDevice()
    {
        _host.Info = new ModelHostInfo("assistant-1.0.0@gpu");

        var status = await Create().GetStatusAsync(default);

        Assert.That(status.Server.Enabled, Is.True);
        Assert.That(status.Server.Available, Is.True);
        Assert.That(status.Server.Device, Is.EqualTo("gpu"));
        Assert.That(status.Server.Reason, Is.Null);
        Assert.That(status.Server.TimeoutSeconds, Is.EqualTo(20));
    }

    [Test]
    public async Task Status_HostNotRunning_IsEnabledButNotAvailable()
    {
        _host.Info = null;

        var status = await Create().GetStatusAsync(default);

        Assert.That(status.Server.Enabled, Is.True);
        Assert.That(status.Server.Available, Is.False);
        Assert.That(status.Server.Device, Is.Null);
        Assert.That(status.Server.Reason, Is.EqualTo("host_unreachable"));
    }

    [TestCase("assistant-0.9.0@cpu")]
    [TestCase("assistant-1.0.0")]
    [TestCase("assistant-1.0.0@tpu")]
    [TestCase("investigator-1.0.0@cpu")]
    public async Task Status_HostAliasWithoutTheLockedVersion_IsNotAvailable(string alias)
    {
        _host.Info = new ModelHostInfo(alias);

        var status = await Create().GetStatusAsync(default);

        Assert.That(status.Server.Available, Is.False);
        Assert.That(status.Server.Reason, Is.EqualTo("model_alias_mismatch"));
    }

    [Test]
    public async Task Status_RetrievalOnlyOrder_TurnsBothTiersOff()
    {
        var status = await Create(b => b.Order = HelpSettings.OrderRetrievalOnly).GetStatusAsync(default);

        Assert.That(status.Order, Is.EqualTo("retrieval-only"));
        Assert.That(status.Browser.Enabled, Is.False);
        Assert.That(status.Server.Enabled, Is.False);
    }

    [Test]
    public async Task Status_ServerFirstOrderAndBrowserDisabled_KeepsTheConfiguredOrder()
    {
        var status = await Create(b => { b.Order = HelpSettings.OrderServerFirst; b.BrowserEnabled = false; }).GetStatusAsync(default);

        Assert.That(status.Order, Is.EqualTo("server-first"));
        Assert.That(status.ConfiguredOrder, Is.EqualTo("server-first"));
        Assert.That(status.Browser.Enabled, Is.False);
        Assert.That(status.Server.Enabled, Is.True);
    }

    [Test]
    public async Task Status_IncompatiblePack_ForcesRetrievalOnlyWhateverTheOrder()
    {
        _files.WriteLock(protocolSha: new string('f', 64));

        var status = await Create(b => b.Order = HelpSettings.OrderServerFirst).GetStatusAsync(default);

        Assert.That(status.Compatible, Is.False);
        Assert.That(status.Reason, Is.EqualTo("protocol_mismatch"));
        Assert.That(status.Order, Is.EqualTo("retrieval-only"));
        Assert.That(status.ConfiguredOrder, Is.EqualTo("server-first"));
        Assert.That(status.Browser.Enabled, Is.False);
        Assert.That(status.Server.Enabled, Is.False);
        Assert.That(status.Pack.Loaded, Is.True);
    }

    [Test]
    public async Task Status_NoPackAndNoLock_IsRetrievalOnlyWithoutFailing()
    {
        var runtime = HelpRuntime.Load(new HelpSettings { ServerEnabled = true }, Path.GetTempPath(), new CapturingLogger<HelpRuntime>());

        var status = await new HelpAssistant(runtime, _host).GetStatusAsync(default);

        Assert.That(status.Order, Is.EqualTo("retrieval-only"));
        Assert.That(status.Reason, Is.EqualTo("no_pack"));
        Assert.That(status.Pack.Loaded, Is.False);
        Assert.That(status.Pack.Sha256, Is.Empty);
        Assert.That(status.Model, Is.Null);
        Assert.That(status.Server.Enabled, Is.False);
    }

    [Test]
    public async Task Status_GapsSwitchedOff_IsReported()
    {
        Assert.That((await Create(b => b.GapsEnabled = false).GetStatusAsync(default)).GapsEnabled, Is.False);
    }

    [Test]
    public async Task Status_TheHostIsProbedOncePerCacheWindow()
    {
        var counting = new CountingHost();
        var runtime = HelpRuntime.Load(_files.Settings(), Path.GetTempPath(), new CapturingLogger<HelpRuntime>());
        var assistant = new HelpAssistant(runtime, counting, TimeSpan.FromMinutes(1));

        await assistant.GetStatusAsync(default);
        await assistant.GetStatusAsync(default);

        Assert.That(counting.Probes, Is.EqualTo(1));
    }

    private sealed class CountingHost : IModelHost
    {
        public int Probes;
        public Task<ModelHostInfo?> GetInfoAsync(CancellationToken cancellationToken)
        {
            Probes++;
            return Task.FromResult<ModelHostInfo?>(new ModelHostInfo("assistant-1.0.0@cpu"));
        }
        public Task<IAsyncEnumerable<string>> StartAsync(ModelChatRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    // ---------------------------------------------------------------- answer

    [Test]
    public async Task Answer_StreamsTheDeltasThenTheParsedAnswer()
    {
        _host.Deltas = ["Click \"New Task\" ", "on the Tasks page.\n", "Source: [1]"];

        var events = await Run(await Create().StartAnswerAsync(Request(), default));

        Assert.That(events.Take(3).Select(e => e.Text), Is.EqualTo(new[] { "Click \"New Task\" ", "on the Tasks page.\n", "Source: [1]" }));
        Assert.That(events.Take(3).Select(e => e.Type), Is.All.EqualTo("delta"));
        var done = events[3];
        Assert.That(events, Has.Count.EqualTo(4));
        Assert.That(done.Type, Is.EqualTo("done"));
        Assert.That(done.Outcome, Is.EqualTo("answered"));
        Assert.That(done.Cited, Is.EqualTo(new[] { 1 }));
        Assert.That(done.Body, Is.EqualTo("Click \"New Task\" on the Tasks page."));
        Assert.That(done.Model, Is.EqualTo("assistant-1.0.0@cpu"));
        Assert.That(done.Device, Is.EqualTo("cpu"));
    }

    [Test]
    public async Task Answer_SendsThePromptOfTheProtocolWithTheDecodingAndTheLockKwargs()
    {
        _host.Deltas = ["x\nSource: [1]"];

        await Run(await Create().StartAnswerAsync(Request("  how do i\n make a   task "), default));

        var sent = _host.LastRequest!;
        var (_, content) = HelpFiles.Protocol();
        var protocol = AssistantProtocol.FromJson(content);
        var expected = protocol.RenderMessages("en", "TaskFlow",
        [
            new ProtocolSection("Create a task - Steps", "1. Open the Tasks page.\n2. Click \"New Task\".\n3. Click \"Create Task\"."),
            new ProtocolSection("Create a task", "Tasks are created from the Tasks page.")
        ], "how do i make a task");
        Assert.That(sent.System, Is.EqualTo(expected.System));
        Assert.That(sent.User, Is.EqualTo(expected.User));
        Assert.That(sent.User, Does.EndWith("Question: how do i make a task"));
        Assert.That(sent.Alias, Is.EqualTo("assistant-1.0.0@cpu"));
        Assert.That(sent.Temperature, Is.Zero);
        Assert.That(sent.MaxTokens, Is.EqualTo(200));
        Assert.That((bool)sent.ChatTemplateKwargs!["enable_thinking"]!, Is.False);
    }

    [Test]
    public async Task Answer_TheRefusalSentenceIsOutcomeRefused()
    {
        _host.Deltas = ["I can't find this in the manual."];

        var events = await Run(await Create().StartAnswerAsync(Request(), default));

        Assert.That(events.Last().Outcome, Is.EqualTo("refused"));
        Assert.That(events.Last().Cited, Is.Empty);
    }

    [TestCase("Click New Task.")]
    [TestCase("Click New Task.\nSource: [3]")]
    [TestCase("")]
    public async Task Answer_AnOutputWithoutAValidSource_IsOutcomeInvalid(string output)
    {
        _host.Deltas = [output];

        var events = await Run(await Create().StartAnswerAsync(Request(), default));

        Assert.That(events.Last().Type, Is.EqualTo("done"));
        Assert.That(events.Last().Outcome, Is.EqualTo("invalid"));
    }

    [Test]
    public async Task Answer_WrongPackHash_IsRefusedBeforeAnyCall()
    {
        var request = Request();
        request.PackSha256 = new string('0', 64);

        var e = await Fails(Create().StartAnswerAsync(request, default));

        Assert.That(e.Failure, Is.EqualTo(HelpAnswerFailure.PackChanged));
        Assert.That(_host.Starts, Is.Zero);
    }

    [Test]
    public async Task Answer_PackHashOfAnotherLanguage_IsRefused()
    {
        var request = Request();
        request.Lang = "it";

        var e = await Fails(Create().StartAnswerAsync(request, default));

        Assert.That(e.Failure, Is.EqualTo(HelpAnswerFailure.PackChanged));
    }

    [Test]
    public async Task Answer_TheItalianPackUsesTheItalianProtocolLanguage()
    {
        var it = _files.WritePack("it");
        _host.Deltas = ["Non trovo questa informazione nel manuale."];
        var request = Request();
        (request.Lang, request.PackSha256) = ("it", it);

        var events = await Run(await Create().StartAnswerAsync(request, default));

        Assert.That(events.Last().Outcome, Is.EqualTo("refused"));
        Assert.That(_host.LastRequest!.System, Does.Contain("Answer in Italian.").And.Contain("Non trovo questa informazione nel manuale."));
    }

    [TestCase(null, "q", "sha")]
    [TestCase("xx", "q", "sha")]
    [TestCase("en", "   ", "sha")]
    [TestCase("en", "q", null)]
    public async Task Answer_MalformedRequest_IsInvalid(string? lang, string? question, string? sha)
    {
        var request = Request();
        (request.Lang, request.Question, request.PackSha256) = (lang, question, sha == "sha" ? _packSha : sha);

        var e = await Fails(Create().StartAnswerAsync(request, default));

        Assert.That(e.Failure, Is.EqualTo(HelpAnswerFailure.InvalidRequest));
    }

    [Test]
    public async Task Answer_UnknownRepeatedOrNoSections_AreInvalid()
    {
        foreach (var ids in new List<string>?[] { null, [], ["nope#x"], [HelpFiles.SectionA, HelpFiles.SectionA] })
        {
            var request = Request();
            request.SectionIds = ids;

            var e = await Fails(Create().StartAnswerAsync(request, default));

            Assert.That(e.Failure, Is.EqualTo(HelpAnswerFailure.InvalidRequest));
        }
    }

    [Test]
    public async Task Answer_ServerTierOff_IsUnavailable()
    {
        var e = await Fails(Create(b => b.ServerEnabled = false).StartAnswerAsync(Request(), default));

        Assert.That(e.Failure, Is.EqualTo(HelpAnswerFailure.Unavailable));
    }

    [Test]
    public async Task Answer_RetrievalOnlyOrder_IsUnavailable()
    {
        var e = await Fails(Create(b => b.Order = HelpSettings.OrderRetrievalOnly).StartAnswerAsync(Request(), default));

        Assert.That(e.Failure, Is.EqualTo(HelpAnswerFailure.Unavailable));
    }

    [Test]
    public async Task Answer_IncompatiblePack_IsUnavailable()
    {
        _files.WriteLock(protocolSha: new string('e', 64));

        var e = await Fails(Create().StartAnswerAsync(Request(), default));

        Assert.That(e.Failure, Is.EqualTo(HelpAnswerFailure.Unavailable));
        Assert.That(_host.Starts, Is.Zero);
    }

    [Test]
    public async Task Answer_HostWithAnotherModelVersion_IsUnavailable()
    {
        _host.Info = new ModelHostInfo("assistant-0.9.0@cpu");

        var e = await Fails(Create().StartAnswerAsync(Request(), default));

        Assert.That(e.Failure, Is.EqualTo(HelpAnswerFailure.Unavailable));
        Assert.That(_host.Starts, Is.Zero);
    }

    [Test]
    public async Task Answer_HostThatRefusesTheRequest_IsUnavailable()
    {
        _host.StartFailure = new ModelHostUnavailableException("down");

        var e = await Fails(Create().StartAnswerAsync(Request(), default));

        Assert.That(e.Failure, Is.EqualTo(HelpAnswerFailure.Unavailable));
    }

    [Test]
    public async Task Answer_HostThatDoesNotAcceptInTime_IsATimeout()
    {
        _host.HangOnStart = true;

        var e = await Fails(Create(b => b.ServerTimeoutSeconds = 0).StartAnswerAsync(Request(), default));

        Assert.That(e.Failure, Is.EqualTo(HelpAnswerFailure.Timeout));
    }

    [Test]
    public async Task Answer_HostThatStopsMidAnswer_EndsTheStreamWithATimeoutError()
    {
        _host.Deltas = ["Click "];
        _host.HangAfterDeltas = true;

        var events = await Run(await Create(b => b.ServerTimeoutSeconds = 0).StartAnswerAsync(Request(), default));

        Assert.That(events.Select(e => e.Type), Is.EqualTo(new[] { "delta", "error" }));
        Assert.That(events.Last().Reason, Is.EqualTo("timeout"));
    }

    [Test]
    public async Task Answer_AllSlotsTaken_IsBusyAfterTheQueueTimeoutAndASlotFreesWhenTheStreamIsDisposed()
    {
        _host.Deltas = ["x\nSource: [1]"];
        var assistant = Create(b => { b.ServerMaxConcurrent = 1; b.ServerQueueTimeoutSeconds = 0; });
        var first = await assistant.StartAnswerAsync(Request(), default);

        var busy = await Fails(assistant.StartAnswerAsync(Request(), default));
        Assert.That(busy.Failure, Is.EqualTo(HelpAnswerFailure.Busy));

        await Run(first);
        var second = await assistant.StartAnswerAsync(Request(), default);
        await Run(second);
    }

    [Test]
    public async Task Answer_MaxConcurrentGenerationsRunAtOnce()
    {
        _host.Deltas = ["x\nSource: [1]"];
        var assistant = Create(b => { b.ServerMaxConcurrent = 2; b.ServerQueueTimeoutSeconds = 0; });

        var a = await assistant.StartAnswerAsync(Request(), default);
        var b2 = await assistant.StartAnswerAsync(Request(), default);
        var third = await Fails(assistant.StartAnswerAsync(Request(), default));

        Assert.That(third.Failure, Is.EqualTo(HelpAnswerFailure.Busy));
        await using (a) { }
        await using (b2) { }
    }

    [Test]
    public async Task Answer_ARequestWaitingForASlotIsServedWhenOneFreesInTime()
    {
        _host.Deltas = ["x\nSource: [1]"];
        var assistant = Create(b => { b.ServerMaxConcurrent = 1; b.ServerQueueTimeoutSeconds = 5; });
        var first = await assistant.StartAnswerAsync(Request(), default);

        var waiting = assistant.StartAnswerAsync(Request(), default);
        await Task.Delay(50);
        Assert.That(waiting.IsCompleted, Is.False);
        await Run(first);

        var second = await waiting;
        Assert.That((await Run(second)).Last().Outcome, Is.EqualTo("answered"));
    }

    [Test]
    public async Task Answer_TheSelectionOfTheProtocolCapsTheContext()
    {
        var protocol = AssistantProtocol.FromJson(HelpFiles.Protocol().Content);
        _host.Deltas = ["x\nSource: [1]"];
        var request = Request();
        request.SectionIds = Enumerable.Repeat(HelpFiles.SectionA, 1).ToList();

        await Run(await Create().StartAnswerAsync(request, default));

        Assert.That(protocol.MaxSections, Is.EqualTo(4));
        Assert.That(_host.LastRequest!.User, Does.Contain("[1] Create a task - Steps").And.Not.Contain("[2]"));
    }
}
