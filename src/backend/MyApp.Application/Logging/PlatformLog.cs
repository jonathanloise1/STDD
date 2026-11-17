using Microsoft.Extensions.Logging;

namespace MyApp.Application.Logging;

/// <summary>
/// Platform events (operations.md 3.5): entries the backend owns that no test case defines.
/// Names start with <c>platform.</c>; they are not log keys and carry no story comment.
/// </summary>
public static partial class PlatformLog
{
    public static class Keys
    {
        public const string PlatformRequestUnhandled = "platform.request.unhandled";
        public const string PlatformRequestRejected = "platform.request.rejected";
        public const string PlatformRequestRateLimited = "platform.request.rate_limited";
        public const string PlatformJobFailed = "platform.job.failed";
        public const string PlatformHealthUnhealthy = "platform.health.unhealthy";
        public const string PlatformStartupFailed = "platform.startup.failed";
    }

    [LoggerMessage(EventId = 1, EventName = Keys.PlatformRequestUnhandled, Level = LogLevel.Error,
        Message = "Request failed with an unhandled exception: {ExceptionType} at {TopFrame} on {Route}")]
    public static partial void PlatformRequestUnhandled(this ILogger logger, Exception exception, string exceptionType, string topFrame, string route);

    [LoggerMessage(EventId = 2, EventName = Keys.PlatformRequestRejected, Level = LogLevel.Warning,
        Message = "Request rejected: {ExceptionType} at {TopFrame} on {Route}")]
    public static partial void PlatformRequestRejected(this ILogger logger, string exceptionType, string topFrame, string route);

    [LoggerMessage(EventId = 3, EventName = Keys.PlatformRequestRateLimited, Level = LogLevel.Warning,
        Message = "Request rate limited by policy {Policy}")]
    public static partial void PlatformRequestRateLimited(this ILogger logger, string policy);

    [LoggerMessage(EventId = 4, EventName = Keys.PlatformJobFailed, Level = LogLevel.Error,
        Message = "Background job {Job} failed: {ExceptionType} at {TopFrame}")]
    public static partial void PlatformJobFailed(this ILogger logger, Exception exception, string job, string exceptionType, string topFrame);

    [LoggerMessage(EventId = 5, EventName = Keys.PlatformHealthUnhealthy, Level = LogLevel.Error,
        Message = "Health check {Check} is not healthy")]
    public static partial void PlatformHealthUnhealthy(this ILogger logger, Exception exception, string check);

    [LoggerMessage(EventId = 6, EventName = Keys.PlatformStartupFailed, Level = LogLevel.Critical,
        Message = "Application startup failed: {ExceptionType} at {TopFrame}")]
    public static partial void PlatformStartupFailed(this ILogger logger, Exception exception, string exceptionType, string topFrame);
}
