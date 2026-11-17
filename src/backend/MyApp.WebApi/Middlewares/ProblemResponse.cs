using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;

namespace MyApp.WebApi.Middlewares;

/// <summary>
/// The error response of the backend: an RFC 9457 problem document, content type
/// <c>application/problem+json</c> (operations.md 4.2).
/// </summary>
public static class ProblemResponse
{
    public const string ContentType = "application/problem+json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>The trace id of the current request: 32 lower-case hex.</summary>
    public static string CurrentTraceId()
    {
        var activity = Activity.Current;
        return activity is not null && activity.TraceId != default
            ? activity.TraceId.ToHexString()
            : ActivityTraceId.CreateRandom().ToHexString();
    }

    /// <summary>Builds the document. <paramref name="exception"/> is added only when given (Development).</summary>
    public static ProblemDocument Create(int status, string detail, string? code = null,
        IReadOnlyDictionary<string, string[]>? errors = null, Exception? exception = null) => new()
    {
        Title = ReasonPhrases.GetReasonPhrase(status),
        Status = status,
        Detail = detail,
        Code = code,
        TraceId = CurrentTraceId(),
        Errors = errors,
        Exception = exception is null
            ? null
            : new ProblemException { Type = exception.GetType().FullName ?? exception.GetType().Name, Message = exception.Message }
    };

    public static Task WriteAsync(HttpContext context, ProblemDocument document)
    {
        context.Response.StatusCode = document.Status;
        context.Response.ContentType = ContentType;
        return context.Response.Body.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions)).AsTask();
    }
}

/// <summary>Fields in the order of operations.md 4.2.</summary>
public sealed class ProblemDocument
{
    public string Type { get; init; } = "about:blank";
    public string Title { get; init; } = string.Empty;
    public int Status { get; init; }
    public string Detail { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string TraceId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
    public ProblemException? Exception { get; init; }
}

public sealed class ProblemException
{
    public string Type { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
