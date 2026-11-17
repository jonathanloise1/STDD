using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using MyApp.Application.Help;
using Newtonsoft.Json.Linq;

namespace MyApp.WebApi.Help;

/// <summary>
/// The llama.cpp <c>llama-server</c> of models.md 7.4 over HTTP: <c>GET /v1/models</c> for the alias and
/// <c>POST /v1/chat/completions</c> with <c>stream: true</c>. The caller sets the base address (the endpoint of
/// <c>MyApp:Assistant:Server:Endpoint</c>); the time limit is the caller's cancellation token.
/// </summary>
public sealed class ModelHostClient(HttpClient http) : IModelHost
{
    public async Task<ModelHostInfo?> GetInfoAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync("v1/models", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var alias = (string?)json.SelectToken("data[0].id");
            return string.IsNullOrWhiteSpace(alias) ? null : new ModelHostInfo(alias);
        }
        catch (Exception e) when (e is HttpRequestException or Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }

    public async Task<IAsyncEnumerable<string>> StartAsync(ModelChatRequest request, CancellationToken cancellationToken)
    {
        var body = new JObject
        {
            ["model"] = request.Alias,
            ["messages"] = new JArray(
                new JObject { ["role"] = "system", ["content"] = request.System },
                new JObject { ["role"] = "user", ["content"] = request.User }),
            ["temperature"] = request.Temperature,
            ["max_tokens"] = request.MaxTokens,
            ["stream"] = true
        };
        if (request.ChatTemplateKwargs is { } kwargs)
            body["chat_template_kwargs"] = kwargs.DeepClone();

        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json")
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException e)
        {
            throw new ModelHostUnavailableException("The model host cannot be reached.", e);
        }

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new ModelHostUnavailableException($"The model host answered {status}.");
        }

        return Deltas(response, cancellationToken);
    }

    /// <summary>Reads the server-sent events and yields the content of each delta.</summary>
    private static async IAsyncEnumerable<string> Deltas(HttpResponseMessage response, [EnumeratorCancellation] CancellationToken ct)
    {
        using (response)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (true)
            {
                string? line;
                try
                {
                    line = await reader.ReadLineAsync(ct);
                }
                catch (Exception e) when (e is IOException or HttpRequestException)
                {
                    throw new ModelHostUnavailableException("The model host closed the connection.", e);
                }

                if (line is null)
                    yield break;
                if (!line.StartsWith("data:", StringComparison.Ordinal))
                    continue;

                var payload = line[5..].Trim();
                if (payload == "[DONE]")
                    yield break;

                string? content;
                try
                {
                    content = (string?)JObject.Parse(payload).SelectToken("choices[0].delta.content");
                }
                catch (Newtonsoft.Json.JsonException)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(content))
                    yield return content;
            }
        }
    }
}
