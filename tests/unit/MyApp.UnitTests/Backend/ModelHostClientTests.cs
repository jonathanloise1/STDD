extern alias WebApi;

using System.Net;
using System.Text;
using MyApp.Application.Help;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using WebApi::MyApp.WebApi.Help;

namespace MyApp.UnitTests.Backend;

/// <summary>The HTTP side of the model host (models.md 7.4) against a fake <c>llama-server</c>.</summary>
[TestFixture]
[Property("UserStory", "US-HELP-05")]
public class ModelHostClientTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return await respond(request, cancellationToken);
        }
    }

    private static ModelHostClient Client(FakeHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:8091/") });

    private static HttpResponseMessage Sse(params string[] lines) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(string.Join("\n\n", lines) + "\n\n", Encoding.UTF8, "text/event-stream")
    };

    private static string Chunk(string? content) =>
        "data: " + new JObject { ["choices"] = new JArray(new JObject { ["delta"] = content is null ? new JObject() : new JObject { ["content"] = content } }) }.ToString(Newtonsoft.Json.Formatting.None);

    private static readonly ModelChatRequest Request = new("assistant-1.0.0@cpu", "SYSTEM", "USER", new JObject { ["enable_thinking"] = false }, 0, 200);

    [Test]
    public async Task Info_ReadsTheAliasOfTheServedModel()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"object\":\"list\",\"data\":[{\"id\":\"assistant-1.0.0@gpu\",\"object\":\"model\"}]}")
        }));

        var info = await Client(handler).GetInfoAsync(default);

        Assert.That(info, Is.EqualTo(new ModelHostInfo("assistant-1.0.0@gpu")));
        Assert.That(handler.Seen.Single().Request.RequestUri!.AbsolutePath, Is.EqualTo("/v1/models"));
    }

    [TestCase(HttpStatusCode.InternalServerError, "")]
    [TestCase(HttpStatusCode.OK, "not json")]
    [TestCase(HttpStatusCode.OK, "{\"data\":[]}")]
    public async Task Info_AnythingElse_IsNull(HttpStatusCode status, string body)
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }));

        Assert.That(await Client(handler).GetInfoAsync(default), Is.Null);
    }

    [Test]
    public async Task Info_ConnectionRefused_IsNull()
    {
        var handler = new FakeHandler((_, _) => throw new HttpRequestException("refused"));

        Assert.That(await Client(handler).GetInfoAsync(default), Is.Null);
    }

    [Test]
    public async Task Start_PostsTheChatRequestWithTheDecodingAndTheKwargs()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(Sse(Chunk("a"), "data: [DONE]")));

        var deltas = await Client(handler).StartAsync(Request, default);
        await deltas.ToListAsync();

        var (request, body) = handler.Seen.Single();
        Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo("/v1/chat/completions"));
        var json = JObject.Parse(body);
        Assert.That((string)json["model"]!, Is.EqualTo("assistant-1.0.0@cpu"));
        Assert.That((double)json["temperature"]!, Is.Zero);
        Assert.That((int)json["max_tokens"]!, Is.EqualTo(200));
        Assert.That((bool)json["stream"]!, Is.True);
        Assert.That((bool)json["chat_template_kwargs"]!["enable_thinking"]!, Is.False);
        Assert.That(json["messages"]!.Select(m => ((string)m["role"]!, (string)m["content"]!)),
            Is.EqualTo(new[] { ("system", "SYSTEM"), ("user", "USER") }));
    }

    [Test]
    public async Task Start_WithoutKwargs_SendsNone()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(Sse("data: [DONE]")));

        await (await Client(handler).StartAsync(Request with { ChatTemplateKwargs = null }, default)).ToListAsync();

        Assert.That(JObject.Parse(handler.Seen.Single().Body).ContainsKey("chat_template_kwargs"), Is.False);
    }

    [Test]
    public async Task Stream_YieldsTheContentOfEachDeltaAndIgnoresTheRest()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(Sse(
            Chunk(null), ": keep-alive", Chunk("Click "), "data: {not json", Chunk(""), Chunk("\"New Task\".\nSource: [1]"), "data: [DONE]", Chunk("after done"))));

        var deltas = await (await Client(handler).StartAsync(Request, default)).ToListAsync();

        Assert.That(deltas, Is.EqualTo(new[] { "Click ", "\"New Task\".\nSource: [1]" }));
    }

    [Test]
    public async Task Stream_ContentWithAccentsAndEmoji_SurvivesTheTransport()
    {
        var text = "Apri la scheda è \U0001F600";
        var handler = new FakeHandler((_, _) => Task.FromResult(Sse(Chunk(text))));

        var deltas = await (await Client(handler).StartAsync(Request, default)).ToListAsync();

        Assert.That(string.Concat(deltas), Is.EqualTo(text));
    }

    [Test]
    public void Start_HostThatCannotBeReached_IsUnavailable()
    {
        var handler = new FakeHandler((_, _) => throw new HttpRequestException("refused"));

        Assert.ThrowsAsync<ModelHostUnavailableException>(async () => await Client(handler).StartAsync(Request, default));
    }

    [Test]
    public void Start_HostThatAnswersAnError_IsUnavailable()
    {
        var handler = new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        Assert.ThrowsAsync<ModelHostUnavailableException>(async () => await Client(handler).StartAsync(Request, default));
    }

    [Test]
    public void Start_HostThatDoesNotAnswerInTime_IsCancelled()
    {
        var handler = new FakeHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        Assert.CatchAsync<OperationCanceledException>(async () => await Client(handler).StartAsync(Request, cts.Token));
    }

    [Test]
    public async Task TheClientFeedsTheAssistantEndToEnd()
    {
        using var files = new HelpFiles();
        var sha = files.WritePack("en");
        files.WriteLock();
        var handler = new FakeHandler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/v1/models"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[{\"id\":\"assistant-1.0.0@gpu\"}]}") }
            : Sse(Chunk("Open the Tasks page.\n"), Chunk("Source: [1]"), "data: [DONE]")));
        var runtime = HelpRuntime.Load(files.Settings(), Path.GetTempPath(), new CapturingLogger<HelpRuntime>());
        using var assistant = new HelpAssistant(runtime, Client(handler), TimeSpan.Zero);

        var stream = await assistant.StartAnswerAsync(new MyApp.Application.DTOs.Help.HelpAnswerRequest
        {
            Lang = "en", Question = "how do i make a task", SectionIds = [HelpFiles.SectionA], PackSha256 = sha
        }, default);
        await using var _ = stream;
        var events = await stream.Events().ToListAsync();

        Assert.That(events.Last().Outcome, Is.EqualTo("answered"));
        Assert.That(events.Last().Device, Is.EqualTo("gpu"));
        Assert.That(events.Last().Body, Is.EqualTo("Open the Tasks page."));
    }
}
