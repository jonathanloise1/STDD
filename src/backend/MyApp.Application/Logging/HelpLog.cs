using Microsoft.Extensions.Logging;

namespace MyApp.Application.Logging;

/// <summary>Log definitions of the Help area (operations.md 3.1).</summary>
public static partial class HelpLog
{
    public static class Keys
    {
        public const string HelpBrowseLoadFailed = "help.browse.load_failed";
        public const string HelpSearchUnavailable = "help.search.unavailable";
        public const string HelpOpenUnavailable = "help.open.unavailable";
        public const string HelpAskUnavailable = "help.ask.unavailable";
        public const string HelpAnswerDeclined = "help.answer.declined";
        public const string HelpAnswerTimeout = "help.answer.timeout";
        public const string HelpDownloadFailed = "help.download.failed";
    }

    // US-HELP-01
    [ClientReported]
    [LoggerMessage(EventId = 1, EventName = Keys.HelpBrowseLoadFailed, Level = LogLevel.Error,
        Message = "Manual not shown: its content could not be loaded on {Route}")]
    public static partial void HelpBrowseLoadFailed(this ILogger logger, string route);

    // US-HELP-02
    [ClientReported]
    [LoggerMessage(EventId = 2, EventName = Keys.HelpSearchUnavailable, Level = LogLevel.Error,
        Message = "Manual search not run: the help content could not be loaded on {Route}")]
    public static partial void HelpSearchUnavailable(this ILogger logger, string route);

    // US-HELP-03
    [ClientReported]
    [LoggerMessage(EventId = 3, EventName = Keys.HelpOpenUnavailable, Level = LogLevel.Error,
        Message = "Help panel not filled: the help content could not be loaded on {Route}")]
    public static partial void HelpOpenUnavailable(this ILogger logger, string route);

    // US-HELP-04
    [ClientReported]
    [LoggerMessage(EventId = 4, EventName = Keys.HelpAskUnavailable, Level = LogLevel.Error,
        Message = "Question not looked up: the help content could not be loaded on {Route}")]
    public static partial void HelpAskUnavailable(this ILogger logger, string route);

    // US-HELP-05
    [LoggerMessage(EventId = 5, EventName = Keys.HelpAnswerDeclined, Level = LogLevel.Information,
        Message = "Answer not shown: the assistant on tier {Tier} gave outcome {Outcome}, the closest sections were shown instead")]
    public static partial void HelpAnswerDeclined(this ILogger logger, string tier, string outcome);

    // US-HELP-05
    [LoggerMessage(EventId = 6, EventName = Keys.HelpAnswerTimeout, Level = LogLevel.Warning,
        Message = "Answer not shown: the assistant on tier {Tier} took too long, the best matches were shown instead")]
    public static partial void HelpAnswerTimeout(this ILogger logger, string tier);

    // US-HELP-07
    [LoggerMessage(EventId = 7, EventName = Keys.HelpDownloadFailed, Level = LogLevel.Error,
        Message = "Unanswered questions not downloaded: the service did not answer ({ExceptionType} at {TopFrame})")]
    public static partial void HelpDownloadFailed(this ILogger logger, Exception exception, string exceptionType, string topFrame);
}
