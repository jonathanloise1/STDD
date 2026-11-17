using Microsoft.Extensions.Logging;

namespace MyApp.Application.Logging;

/// <summary>Log definitions of the Onboarding area (operations.md 3.1).</summary>
public static partial class OnboardingLog
{
    public static class Keys
    {
        public const string OnboardWizUnavailable = "onboard.wiz.unavailable";
    }

    // US-ONBOARD-02
    [LoggerMessage(EventId = 1, EventName = Keys.OnboardWizUnavailable, Level = LogLevel.Error,
        Message = "Organization not created: the service did not answer ({ExceptionType} at {TopFrame})")]
    public static partial void OnboardWizUnavailable(this ILogger logger, Exception exception, string exceptionType, string topFrame);
}
