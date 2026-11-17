namespace MyApp.Domain.Constants;

/// <summary>Why a question counts as a documentation gap (manual.md 7.4).</summary>
public static class HelpGapOutcomes
{
    public const string NoMatch = "no_match";
    public const string Refused = "refused";
    public const string Invalid = "invalid";
    public const string NotHelpful = "not_helpful";
    public const string Timeout = "timeout";

    public static readonly IReadOnlyList<string> All = [NoMatch, Refused, Invalid, NotHelpful, Timeout];
}
