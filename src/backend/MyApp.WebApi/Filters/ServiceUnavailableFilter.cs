using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MyApp.Application.Exceptions;
using MyApp.Application.Logging;
using MyApp.Infrastructure.Logging;
using MyApp.WebApi.Middlewares;

namespace MyApp.WebApi.Filters;

/// <summary>
/// Marks an action whose failure to reach the database or a dependency is the keyed outcome
/// "the service did not answer" (operations.md 3.1, 4.2). the key is a constant of a
/// log definition class (<c>TasksLog.Keys.TaskCreateUnavailable</c>).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ServiceUnavailableAttribute : TypeFilterAttribute
{
    public ServiceUnavailableAttribute(string key) : base(typeof(ServiceUnavailableFilter))
    {
        Key = key;
        Arguments = [key];
    }

    public string Key { get; }
}

/// <summary>
/// The one place that catches database and dependency failures of the marked actions: it writes the keyed
/// entry through <see cref="UnavailableOutcomes"/> and answers with the problem document (503, <c>code</c> =
/// key). It also honours the Development-only <see cref="FaultSwitch"/>.
/// </summary>
public sealed class ServiceUnavailableFilter : IAsyncActionFilter
{
    private readonly string _key;
    private readonly FaultSwitch _faults;
    private readonly ILogger _logger;
    private readonly IHostEnvironment _environment;

    public ServiceUnavailableFilter(string key, FaultSwitch faults, ILoggerFactory loggerFactory, IHostEnvironment environment)
    {
        _key = key;
        _faults = faults;
        _logger = loggerFactory.CreateLogger<ServiceUnavailableFilter>();
        _environment = environment;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (_faults.IsRequested(context.HttpContext, _key))
        {
            context.Result = Unavailable(new SimulatedOutageException(_key));
            return;
        }

        var executed = await next();
        if (executed.Exception is { } exception && !executed.ExceptionHandled && IsUnavailable(exception))
        {
            executed.Result = Unavailable(exception);
            executed.ExceptionHandled = true;
        }
    }

    /// <summary>True for a failure of the database or of a dependency, however deeply wrapped.</summary>
    public static bool IsUnavailable(Exception exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e is DbException or TimeoutException or HttpRequestException or SimulatedOutageException)
                return true;
        }
        return false;
    }

    private IActionResult Unavailable(Exception exception)
    {
        var summary = ExceptionSummary.From(exception);
        if (!UnavailableOutcomes.Emit(_logger, _key, exception, summary.ExceptionType, summary.TopFrame))
            throw new InvalidOperationException($"'{_key}' is not an unavailable key.");

        return new ProblemResult(ProblemResponse.Create(
            StatusCodes.Status503ServiceUnavailable, OutcomeMessages.ServiceUnavailable, _key,
            exception: _environment.IsDevelopment() ? exception : null));
    }
}
