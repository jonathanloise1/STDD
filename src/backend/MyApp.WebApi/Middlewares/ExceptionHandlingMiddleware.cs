using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Routing;
using MyApp.Application.Exceptions;
using MyApp.Application.Logging;
using MyApp.Infrastructure.Logging;

namespace MyApp.WebApi.Middlewares;

/// <summary>
/// Middleware for global exception handling. Every error becomes an RFC 9457 problem document
/// (operations.md 4.2); the middleware writes a platform log entry only for the exceptions that no
/// keyed log entry covers (operations.md 3.5).
/// </summary>
public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IHostEnvironment _environment = environment ?? throw new ArgumentNullException(nameof(environment));

    /// <summary>Status written (without a body) when the client aborted the request.</summary>
    public const int ClientClosedRequest = 499;

    public const string UnexpectedErrorDetail = "An unexpected error occurred.";

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
                context.Response.StatusCode = ClientClosedRequest;
            return;
        }

        int status;
        string detail;
        string? code = null;
        IReadOnlyDictionary<string, string[]>? errors = null;

        switch (exception)
        {
            case OutcomeException outcome:
                status = outcome.StatusCode;
                detail = outcome.Detail;
                code = outcome.Key;
                errors = outcome.Errors;
                break;

            case ValidationException validation:
                status = StatusCodes.Status400BadRequest;
                detail = string.IsNullOrWhiteSpace(validation.Message) ? "Invalid request." : validation.Message;
                errors = DataAnnotationErrors(validation);
                break;

            case FluentValidation.ValidationException fluent:
                status = StatusCodes.Status400BadRequest;
                var messages = fluent.Errors.Select(e => e.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
                detail = messages.Count > 0 ? string.Join(" ", messages) : "Invalid request.";
                errors = fluent.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                break;

            case KeyNotFoundException:
                status = StatusCodes.Status404NotFound;
                detail = "Resource not found.";
                LogRejected(context, exception);
                break;

            case UnauthorizedAccessException:
                status = StatusCodes.Status403Forbidden;
                detail = "Access denied.";
                LogRejected(context, exception);
                break;

            default:
                status = StatusCodes.Status500InternalServerError;
                detail = UnexpectedErrorDetail;
                var summary = ExceptionSummary.From(exception);
                _logger.PlatformRequestUnhandled(exception, summary.ExceptionType, summary.TopFrame, RouteOf(context));
                break;
        }

        if (context.Response.HasStarted)
            return; // nothing can be written any more; the entry above is the record

        // SECURITY: exception details only in the Development environment
        var document = ProblemResponse.Create(status, detail, code, errors,
            _environment.IsDevelopment() ? exception : null);
        await ProblemResponse.WriteAsync(context, document);
    }

    private void LogRejected(HttpContext context, Exception exception)
    {
        var summary = ExceptionSummary.From(exception);
        _logger.PlatformRequestRejected(summary.ExceptionType, summary.TopFrame, RouteOf(context));
    }

    /// <summary>The route template of the endpoint, never the raw path.</summary>
    public static string RouteOf(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unknown";

    private static IReadOnlyDictionary<string, string[]>? DataAnnotationErrors(ValidationException exception)
    {
        var message = exception.ValidationResult?.ErrorMessage;
        var members = exception.ValidationResult?.MemberNames.ToList();
        if (string.IsNullOrWhiteSpace(message) || members is null || members.Count == 0)
            return null;
        return members.ToDictionary(m => m, _ => new[] { message });
    }
}
