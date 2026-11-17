using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyApp.IntegrationTests.Infrastructure;

/// <summary>The answer of the backend: status, body and the parsed problem document of an error.</summary>
public sealed class ApiResponse
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    public ApiResponse(int status, string body, string? contentType)
    {
        Status = status;
        Body = body;
        ContentType = contentType;
    }

    public int Status { get; }
    public string Body { get; }
    public string? ContentType { get; }

    /// <summary>The body as JSON; throws when it is not JSON.</summary>
    public JsonElement Json => JsonDocument.Parse(Body).RootElement.Clone();

    /// <summary>The body as a problem document (operations.md 4.2).</summary>
    public ProblemDocument Problem => JsonSerializer.Deserialize<ProblemDocument>(Body, ReadOptions)
        ?? throw new InvalidOperationException("The body is not a problem document.");

    /// <summary>The value of a string property of the body.</summary>
    public string? String(string property) => Json.GetProperty(property).GetString();
}

/// <summary>The fields of the error response of operations.md 4.2.</summary>
public sealed class ProblemDocument
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public int Status { get; set; }
    public string? Detail { get; set; }
    public string? Code { get; set; }
    public string? TraceId { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
    public JsonElement? Exception { get; set; }
}

/// <summary>
/// HTTP calls to the running backend. A call names the user it is made as; the impersonation header carries
/// the user's id (development.md 16.1). A call with no user is anonymous.
/// </summary>
public static class Api
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Task<ApiResponse> Get(string path, Guid? asUser = null, params (string Name, string Value)[] headers) =>
        Send(HttpMethod.Get, path, asUser, null, headers);

    public static Task<ApiResponse> Post(string path, object? body, Guid? asUser = null, params (string Name, string Value)[] headers) =>
        Send(HttpMethod.Post, path, asUser, body ?? new { }, headers);

    public static Task<ApiResponse> Put(string path, object body, Guid? asUser = null, params (string Name, string Value)[] headers) =>
        Send(HttpMethod.Put, path, asUser, body, headers);

    public static Task<ApiResponse> Patch(string path, object body, Guid? asUser = null, params (string Name, string Value)[] headers) =>
        Send(HttpMethod.Patch, path, asUser, body, headers);

    public static Task<ApiResponse> Delete(string path, Guid? asUser = null, params (string Name, string Value)[] headers) =>
        Send(HttpMethod.Delete, path, asUser, null, headers);

    /// <summary>The same call with the fault switch asking for the operations with these log keys to fail.</summary>
    public static (string Name, string Value) Fault(params string[] logKeys) =>
        (IntegrationSettings.FaultHeaderName, string.Join(",", logKeys));

    /// <summary>A multipart upload with one file in the field <c>file</c>.</summary>
    public static async Task<ApiResponse> PostFile(
        string path, byte[] content, string contentType, Guid? asUser = null, params (string Name, string Value)[] headers)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", "logo.png");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form };
        return await Execute(request, asUser, headers);
    }

    private static async Task<ApiResponse> Send(
        HttpMethod method, string path, Guid? asUser, object? body, (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body, WriteOptions), Encoding.UTF8, "application/json");
        return await Execute(request, asUser, headers);
    }

    private static async Task<ApiResponse> Execute(
        HttpRequestMessage request, Guid? asUser, (string Name, string Value)[] headers)
    {
        if (asUser is { } id)
            request.Headers.TryAddWithoutValidation(IntegrationSettings.ImpersonationHeaderName, id.ToString());
        foreach (var (name, value) in headers)
            request.Headers.TryAddWithoutValidation(name, value);

        using var response = await BackendHost.Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return new ApiResponse((int)response.StatusCode, text, response.Content.Headers.ContentType?.MediaType);
    }
}
