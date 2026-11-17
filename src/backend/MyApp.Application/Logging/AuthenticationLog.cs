using Microsoft.Extensions.Logging;

namespace MyApp.Application.Logging;

/// <summary>Log definitions of the Authentication area (operations.md 3.1).</summary>
public static partial class AuthenticationLog
{
    public static class Keys
    {
        public const string AuthLoginUnavailable = "auth.login.unavailable";
        public const string AuthLangUnavailable = "auth.lang.unavailable";
    }

    // US-AUTH-01
    [LoggerMessage(EventId = 1, EventName = Keys.AuthLoginUnavailable, Level = LogLevel.Error,
        Message = "Sign-in not completed: the service did not answer when the account was loaded ({ExceptionType} at {TopFrame})")]
    public static partial void AuthLoginUnavailable(this ILogger logger, Exception exception, string exceptionType, string topFrame);

    // US-AUTH-08
    [LoggerMessage(EventId = 2, EventName = Keys.AuthLangUnavailable, Level = LogLevel.Error,
        Message = "Language not saved: the service did not answer ({ExceptionType} at {TopFrame})")]
    public static partial void AuthLangUnavailable(this ILogger logger, Exception exception, string exceptionType, string topFrame);
}
