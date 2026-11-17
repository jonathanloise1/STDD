using System.Text.RegularExpressions;

namespace MyApp.Application.Help;

/// <summary>
/// The masking of a question before it is stored (manual.md 7.4): e-mail addresses become <c>[email]</c>, web
/// addresses <c>[url]</c> and any run of four or more digits, with single spaces, dots or dashes inside,
/// <c>[number]</c>. The panel applies the same rules before it sends.
/// </summary>
public static partial class GapMasking
{
    /// <summary>The longest stored question, in characters after masking.</summary>
    public const int MaxQuestionChars = 300;

    [GeneratedRegex(@"\b(?:https?://|ftp://|www\.)[^\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"[\p{L}\p{N}._%+\-]+@[\p{L}\p{N}\-]+(?:\.[\p{L}\p{N}\-]+)+")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\p{Nd}(?:[ .\-]?\p{Nd}){3,}")]
    private static partial Regex NumberPattern();

    /// <summary>Replaces the addresses and numbers, then cuts at <see cref="MaxQuestionChars"/> code points.</summary>
    public static string Mask(string question)
    {
        var masked = UrlPattern().Replace(question, m =>
        {
            // a sentence ending after the address is not part of it
            var trailing = m.Value.Length - m.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '"', '\'').Length;
            return "[url]" + m.Value[^trailing..][..trailing];
        });
        masked = EmailPattern().Replace(masked, "[email]");
        masked = NumberPattern().Replace(masked, "[number]");
        return AssistantProtocol.CutCodePoints(masked, MaxQuestionChars);
    }
}
