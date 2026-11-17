using Newtonsoft.Json.Linq;

namespace MyApp.Application.Help;

/// <summary>One chat completion request to the model host (models.md 7.4).</summary>
public sealed record ModelChatRequest(string Alias, string System, string User, JObject? ChatTemplateKwargs, double Temperature, int MaxTokens);

/// <summary>The model the host serves, read from <c>GET /v1/models</c>.</summary>
public sealed record ModelHostInfo(string Alias);

/// <summary>The model host cannot be reached or refused the request.</summary>
public sealed class ModelHostUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The llama.cpp host of models.md 7.4. Implemented over HTTP in the web layer.</summary>
public interface IModelHost
{
    /// <summary>The served model; null when the host is unreachable or serves none.</summary>
    Task<ModelHostInfo?> GetInfoAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sends the request and returns the text deltas once the host has accepted it. Throws
    /// <see cref="ModelHostUnavailableException"/> when it cannot; the enumeration ends with the generation.
    /// </summary>
    Task<IAsyncEnumerable<string>> StartAsync(ModelChatRequest request, CancellationToken cancellationToken);
}
