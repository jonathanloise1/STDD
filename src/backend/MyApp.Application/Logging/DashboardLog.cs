using Microsoft.Extensions.Logging;

namespace MyApp.Application.Logging;

/// <summary>Log definitions of the Dashboard area (operations.md 3.1).</summary>
public static partial class DashboardLog
{
    public static class Keys
    {
        public const string DashKpiUnavailable = "dash.kpi.unavailable";
        public const string DashRecentUnavailable = "dash.recent.unavailable";
    }

    // US-DASH-01
    [LoggerMessage(EventId = 1, EventName = Keys.DashKpiUnavailable, Level = LogLevel.Error,
        Message = "Task counts not loaded: the service did not answer ({ExceptionType} at {TopFrame})")]
    public static partial void DashKpiUnavailable(this ILogger logger, Exception exception, string exceptionType, string topFrame);

    // US-DASH-02
    [LoggerMessage(EventId = 2, EventName = Keys.DashRecentUnavailable, Level = LogLevel.Error,
        Message = "Recent tasks not loaded: the service did not answer ({ExceptionType} at {TopFrame})")]
    public static partial void DashRecentUnavailable(this ILogger logger, Exception exception, string exceptionType, string topFrame);
}
