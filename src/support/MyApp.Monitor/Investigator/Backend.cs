using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyApp.Monitor.Investigator;

public sealed record ChatMessage(string Role, string Content);

/// <param name="Grammar">The GBNF text of the allowed turn (11.3): the host decodes under it.</param>
public sealed record GenerationRequest(IReadOnlyList<ChatMessage> Messages, int MaxTokens, string? Grammar);

/// <param name="Alias">What the host serves: <c>{model id}-{model version}@{gpu|cpu}</c> (src/support/model-host).</param>
public sealed record HostInfo(string Alias)
{
    public string ModelAndVersion => Alias.Contains('@') ? Alias[..Alias.IndexOf('@')] : Alias;
    public string Device => Alias.Contains('@') ? Alias[(Alias.IndexOf('@') + 1)..] : "";
}

/// <summary>The model host failed to answer: not reachable, refused, an error status, or an answer that is not a chat completion.</summary>
public sealed class HostException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The only thing the investigator needs from a model runtime (operations.md 14.1): who it is, and the next turn under a grammar.</summary>
public interface IInferenceBackend
{
    /// <summary>Ready and its alias. Throws <see cref="HostException"/> when nothing answers.</summary>
    Task<HostInfo> ProbeAsync(CancellationToken ct);

    /// <summary>The text of the next assistant turn.</summary>
    Task<string> GenerateAsync(GenerationRequest request, CancellationToken ct);
}

public interface IInferenceBackendFactory
{
    IInferenceBackend Create(string tier, Uri endpoint, JsonObject? chatTemplateKwargs);
}

/// <summary>
/// The llama.cpp <c>llama-server</c> (models.md 7.4) over HTTP: <c>GET /health</c>, <c>GET /v1/models</c> and
/// <c>POST /v1/chat/completions</c> with greedy decoding and the <c>grammar</c> field, as the evaluation drives it.
/// </summary>
public sealed class LlamaServerBackend(HttpClient http, Uri endpoint, JsonObject? chatTemplateKwargs, int maxTokens = 700) : IInferenceBackend
{
    public async Task<HostInfo> ProbeAsync(CancellationToken ct)
    {
        try
        {
            using var health = await http.GetAsync(new Uri(endpoint, "/health"), ct).ConfigureAwait(false);
            if (!health.IsSuccessStatusCode) throw new HostException($"the host at {endpoint} is not ready (health answered {(int)health.StatusCode})");
            using var models = await http.GetAsync(new Uri(endpoint, "/v1/models"), ct).ConfigureAwait(false);
            if (!models.IsSuccessStatusCode) throw new HostException($"the host at {endpoint} answered {(int)models.StatusCode} to /v1/models");
            var body = await models.Content.ReadFromJsonAsync<JsonNode>(ct).ConfigureAwait(false);
            var id = MonitorJson.ScalarText(body?["data"]?[0]?["id"]) ?? MonitorJson.ScalarText(body?["models"]?[0]?["name"]);
            return string.IsNullOrWhiteSpace(id) ? throw new HostException($"the host at {endpoint} names no model") : new HostInfo(id);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new HostException($"no host answers at {endpoint}: {ex.Message}", ex);
        }
    }

    public async Task<string> GenerateAsync(GenerationRequest request, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["messages"] = new JsonArray(request.Messages.Select(m => (JsonNode?)new JsonObject { ["role"] = m.Role, ["content"] = m.Content }).ToArray()),
            ["temperature"] = 0, ["top_k"] = 1, ["seed"] = 0, ["max_tokens"] = request.MaxTokens > 0 ? request.MaxTokens : maxTokens, ["stream"] = false,
        };
        if (chatTemplateKwargs is { Count: > 0 }) body["chat_template_kwargs"] = chatTemplateKwargs.DeepClone();
        if (request.Grammar is not null) body["grammar"] = request.Grammar;
        try
        {
            using var content = new StringContent(body.ToJsonString(MonitorJson.Compact), Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(new Uri(endpoint, "/v1/chat/completions"), content, ct).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new HostException($"the host answered {(int)response.StatusCode}: {(text.Length > 200 ? text[..200] : text)}");
            var answer = MonitorJson.ScalarText(JsonNode.Parse(text)?["choices"]?[0]?["message"]?["content"]);
            return answer ?? throw new HostException("the host's answer has no message content");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            throw new HostException($"the host at {endpoint} failed: {ex.Message}", ex);
        }
    }
}

public sealed class LlamaServerBackendFactory(IHttpClientFactory clients, Protocol protocol) : IInferenceBackendFactory
{
    public const string ClientName = "monitor-investigator";

    public IInferenceBackend Create(string tier, Uri endpoint, JsonObject? chatTemplateKwargs) =>
        new LlamaServerBackend(clients.CreateClient(ClientName), endpoint, chatTemplateKwargs, protocol.MaxNewTokens);
}
