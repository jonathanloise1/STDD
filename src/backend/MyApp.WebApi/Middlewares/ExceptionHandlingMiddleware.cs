using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;

namespace MyApp.WebApi.Middlewares;

/// <summary>
/// Middleware for global exception handling.
/// </summary>
public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly ILogger<ExceptionHandlingMiddleware> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IHostEnvironment _environment = environment ?? throw new ArgumentNullException(nameof(environment));

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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
        HttpStatusCode statusCode;
        string message;

        switch (exception)
        {
            case System.ComponentModel.DataAnnotations.ValidationException validationException:
                statusCode = HttpStatusCode.BadRequest;
                message = string.IsNullOrWhiteSpace(validationException.Message)
                    ? "Invalid request."
                    : validationException.Message;
                break;

            case KeyNotFoundException:
                statusCode = HttpStatusCode.NotFound;
                message = "Resource not found.";
                break;

            case UnauthorizedAccessException:
                statusCode = HttpStatusCode.Forbidden;
                message = "Access denied.";
                break;

            default:
                statusCode = HttpStatusCode.InternalServerError;
                message = "An unexpected error occurred.";
                break;
        }

        if ((int)statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled server error: {Message}", message);
        }
        else if ((int)statusCode >= 400)
        {
            _logger.LogWarning(exception, "Client error: {Message}", message);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        // SECURITY: Only expose exception details in Development environment
        if (_environment.IsDevelopment())
        {
            message = exception.Message;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(message, _jsonOptions));
    }
}
