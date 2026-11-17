using Microsoft.Extensions.Logging;

namespace MyApp.Application.Logging;

/// <summary>
/// Log definitions of the Audit area (operations.md 3.1). Named <c>AuditTrailLog</c> because
/// <c>AuditLog</c> is the entity of the audit trail.
/// </summary>
public static partial class AuditTrailLog
{
    public static class Keys
    {
        public const string AudListUnavailable = "aud.list.unavailable";
    }

    // US-AUD-01
    [LoggerMessage(EventId = 1, EventName = Keys.AudListUnavailable, Level = LogLevel.Error,
        Message = "Audit trail not loaded: the service did not answer ({ExceptionType} at {TopFrame})")]
    public static partial void AudListUnavailable(this ILogger logger, Exception exception, string exceptionType, string topFrame);
}
