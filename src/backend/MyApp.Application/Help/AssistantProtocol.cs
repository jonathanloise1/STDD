using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace MyApp.Application.Help;

/// <summary>What the parser found in a model output (models.md 3.4).</summary>
public enum AnswerOutcome
{
    Refused,
    Answered,
    Invalid
}

/// <summary>The result of <see cref="AssistantProtocol.ParseAnswer"/>; <c>Cited</c> is empty unless answered.</summary>
public sealed record ParsedAnswer(AnswerOutcome Outcome, IReadOnlyList<int> Cited, string? Body = null);

/// <summary>The two messages of a prompt and the normalised question that was substituted.</summary>
public sealed record RenderedMessages(string System, string User, string Question);

/// <summary>A section as the protocol sees it: a title and a text.</summary>
public readonly record struct ProtocolSection(string Title, string Text);

/// <summary>
/// The assistant protocol of models.md 3: renderer, context selection and parser. The definition is the
/// <c>protocol.json</c> copy that the help pack carries (models.md 3.1), so the prompt is the one the model was
/// trained with. Lengths count Unicode code points; whitespace is the JavaScript <c>\s</c> set. The golden cases of
/// <c>training/assistant/protocol.golden.json</c> must hold byte for byte (models.md 3.7).
/// </summary>
public sealed partial class AssistantProtocol
{
    private readonly string _system;
    private readonly string _user;
    private readonly string _section;
    private readonly string _sectionSeparator;
    private readonly Dictionary<string, (string LanguageName, string Refusal)> _languages;

    private AssistantProtocol(JObject json)
    {
        SchemaVersion = (int)json["schema_version"]!;
        _system = (string)json["system"]!;
        _user = (string)json["user"]!;
        _section = (string)json["section"]!;
        _sectionSeparator = (string)json["section_separator"]!;
        _languages = ((JObject)json["languages"]!).Properties().ToDictionary(
            p => p.Name,
            p => ((string)p.Value["language_name"]!, (string)p.Value["refusal"]!),
            StringComparer.Ordinal);

        var context = (JObject)json["context"]!;
        MaxSections = (int)context["max_sections"]!;
        MaxSectionChars = (int)context["max_section_chars"]!;
        MaxContextChars = (int)context["max_context_chars"]!;
        MaxQuestionChars = (int)context["max_question_chars"]!;
        ScanLimit = (int)context["scan_limit"]!;

        var answer = (JObject)json["answer"]!;
        SourcePrefix = (string)answer["source_prefix"]!;
        MaxBodyChars = (int)answer["max_body_chars"]!;
        MaxBodyLines = (int)answer["max_body_lines"]!;

        var decoding = (JObject)json["decoding"]!;
        Temperature = (double)decoding["temperature"]!;
        MaxNewTokens = (int)decoding["max_new_tokens"]!;
    }

    public int SchemaVersion { get; }
    public int MaxSections { get; }
    public int MaxSectionChars { get; }
    public int MaxContextChars { get; }
    public int MaxQuestionChars { get; }
    public int ScanLimit { get; }
    public string SourcePrefix { get; }
    public int MaxBodyChars { get; }
    public int MaxBodyLines { get; }

    /// <summary>Decoding of models.md 3.8: greedy, at most <see cref="MaxNewTokens"/> new tokens.</summary>
    public double Temperature { get; }
    public int MaxNewTokens { get; }

    /// <summary>The language codes of the protocol.</summary>
    public IReadOnlyCollection<string> Languages => _languages.Keys;

    /// <summary>Reads the definition (the content of <c>protocol.json</c>, or the pack's byte-equivalent copy).</summary>
    public static AssistantProtocol FromJson(string json) => FromJson(JObject.Parse(json));

    /// <summary>Reads the definition from its parsed form; throws when a field is missing.</summary>
    public static AssistantProtocol FromJson(JObject json)
    {
        try
        {
            return new AssistantProtocol(json);
        }
        catch (Exception e) when (e is NullReferenceException or InvalidCastException or ArgumentException or FormatException)
        {
            throw new FormatException("The assistant protocol definition is incomplete or malformed.", e);
        }
    }

    public bool HasLanguage(string lang) => _languages.ContainsKey(lang);

    public string Refusal(string lang) => _languages[lang].Refusal;

    // ---------------------------------------------------------------- normalisation (3.7)

    /// <summary>The set of the JavaScript <c>\s</c> class, which the protocol fixes for trimming and collapsing.</summary>
    public static bool IsWhitespace(char c) =>
        c is (>= (char)0x0009 and <= (char)0x000D) or (char)0x0020 or (char)0x00A0 or (char)0x1680
            or (>= (char)0x2000 and <= (char)0x200A) or (char)0x2028 or (char)0x2029 or (char)0x202F
            or (char)0x205F or (char)0x3000 or (char)0xFEFF;

    /// <summary>Unicode NFC, then <c>\r\n</c> to <c>\n</c>.</summary>
    public static string NormalizeText(string value) =>
        value.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>The number of Unicode code points.</summary>
    public static int CodePointLength(string value)
    {
        var count = 0;
        foreach (var _ in value.EnumerateRunes())
            count++;
        return count;
    }

    /// <summary>The first <paramref name="max"/> code points.</summary>
    public static string CutCodePoints(string value, int max)
    {
        var count = 0;
        var index = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            if (count == max)
                return value[..index];
            count++;
            index += rune.Utf16SequenceLength;
        }
        return value;
    }

    /// <summary>NFC, CRLF, trim, whitespace runs to one space, cut at the question limit in code points.</summary>
    public string NormalizeQuestion(string question)
    {
        var text = NormalizeText(question);
        var collapsed = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (IsWhitespace(c))
            {
                pendingSpace = collapsed.Length > 0;
                continue;
            }
            if (pendingSpace)
            {
                collapsed.Append(' ');
                pendingSpace = false;
            }
            collapsed.Append(c);
        }
        return CutCodePoints(collapsed.ToString(), MaxQuestionChars);
    }

    [GeneratedRegex(@"\{(app_name|refusal|language_name|sections|n|title|text|question)\}")]
    private static partial Regex PlaceholderPattern();

    /// <summary>One left-to-right pass; substituted values are never scanned again.</summary>
    private static string Substitute(string template, IReadOnlyDictionary<string, string> values) =>
        PlaceholderPattern().Replace(template, m => values.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

    // ---------------------------------------------------------------- rendering (3.2)

    /// <summary>The system and user messages for sections already in context order.</summary>
    public RenderedMessages RenderMessages(string lang, string appName, IReadOnlyList<ProtocolSection> sections, string question)
    {
        var (languageName, refusal) = _languages[lang];
        var system = Substitute(_system, new Dictionary<string, string>
        {
            ["app_name"] = NormalizeText(appName),
            ["refusal"] = refusal,
            ["language_name"] = languageName
        });

        var rendered = string.Join(_sectionSeparator, sections.Select((s, i) => Substitute(_section, new Dictionary<string, string>
        {
            ["n"] = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["title"] = NormalizeText(s.Title),
            ["text"] = NormalizeText(s.Text)
        })));

        var normalized = NormalizeQuestion(question);
        var user = Substitute(_user, new Dictionary<string, string>
        {
            ["sections"] = rendered,
            ["question"] = normalized
        });
        return new RenderedMessages(system, user, normalized);
    }

    // ---------------------------------------------------------------- context selection (3.5)

    /// <summary>
    /// Whole sections only, in rank order, within the budget. <paramref name="maxSections"/> can lower, never raise,
    /// the limit of the protocol.
    /// </summary>
    public List<T> SelectSections<T>(IReadOnlyList<T> ranked, Func<T, string> textOf, int? maxSections = null)
    {
        var limit = maxSections is { } m ? Math.Min(m, MaxSections) : MaxSections;
        var selected = new List<T>();
        var total = 0;
        foreach (var section in ranked.Take(ScanLimit))
        {
            if (selected.Count == limit)
                break;
            var length = CodePointLength(NormalizeText(textOf(section)));
            if (total + length > MaxContextChars)
                continue;
            selected.Add(section);
            total += length;
        }
        return selected;
    }

    // ---------------------------------------------------------------- parsing (3.3, 3.4)

    [GeneratedRegex(@"^Source: \[[1-9]\](?:, \[[1-9]\])*$")]
    private static partial Regex SourceLinePattern();

    [GeneratedRegex(@"\[([1-9])\]")]
    private static partial Regex ReferencePattern();

    /// <summary>Parses a model output against <paramref name="k"/> sections in the context.</summary>
    public ParsedAnswer ParseAnswer(string lang, string output, int k)
    {
        var text = TrimEnd(output.Replace("\r\n", "\n", StringComparison.Ordinal));
        var refusal = _languages[lang].Refusal;
        if (string.Equals(text, refusal, StringComparison.Ordinal))
            return new ParsedAnswer(AnswerOutcome.Refused, []);
        if (text.StartsWith(refusal, StringComparison.Ordinal))
            return Invalid;

        var lines = text.Split('\n');
        if (lines.Length < 2)
            return Invalid;

        var last = lines[^1];
        if (!last.StartsWith(SourcePrefix, StringComparison.Ordinal) || !SourceLinePattern().IsMatch(last))
            return Invalid;

        var refs = ReferencePattern().Matches(last).Select(m => m.Groups[1].Value[0] - '0').ToList();
        for (var i = 1; i < refs.Count; i++)
        {
            if (refs[i] <= refs[i - 1])
                return Invalid;
        }
        if (refs[^1] > k)
            return Invalid;

        var bodyLines = lines[..^1];
        if (bodyLines.Length < 1 || bodyLines.Length > MaxBodyLines)
            return Invalid;
        var body = string.Join('\n', bodyLines);
        if (CodePointLength(body) > MaxBodyChars)
            return Invalid;
        if (body.All(IsWhitespace))
            return Invalid;
        if (bodyLines.Any(l => l.StartsWith("Source:", StringComparison.Ordinal)))
            return Invalid;

        return new ParsedAnswer(AnswerOutcome.Answered, refs, body);
    }

    private static ParsedAnswer Invalid { get; } = new(AnswerOutcome.Invalid, []);

    private static string TrimEnd(string value)
    {
        var end = value.Length;
        while (end > 0 && IsWhitespace(value[end - 1]))
            end--;
        return value[..end];
    }
}
