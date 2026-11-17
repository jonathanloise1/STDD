using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MyApp.Monitor.Sources;

namespace MyApp.Monitor.Analysis;

/// <summary>The five parts of a template id and the id itself (operations.md 6.5).</summary>
public sealed record TemplateInfo(
    string? EventName, string Category, string? ExceptionType, string? TopFrame, string Discriminator, string Canonical, string Id)
{
    public IReadOnlyList<(string Name, string Value)> Parts =>
    [
        ("event_name", EventName ?? ""),
        ("category", Category),
        ("exception_type", ExceptionType ?? ""),
        ("top_frame", TopFrame ?? ""),
        ("discriminator", Discriminator),
    ];
}

public static class TemplateExtractor
{
    /// <summary>Discriminator properties of the events of operations.md 3.5, in table order.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> DiscriminatorProperties = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["platform.request.unhandled"] = ["ExceptionType", "TopFrame", "Route"],
        ["platform.request.rejected"] = ["ExceptionType", "TopFrame", "Route"],
        ["platform.request.rate_limited"] = ["Policy"],
        ["platform.job.failed"] = ["Job", "ExceptionType", "TopFrame"],
        ["platform.health.unhealthy"] = ["Check"],
        ["platform.startup.failed"] = ["ExceptionType", "TopFrame"],
        ["monitor.api.unreachable"] = ["Url"],
        ["monitor.api.unhealthy"] = ["Check"],
    };

    private const int MaxTemplateLength = 300;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static Regex R(string pattern) => new(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled, RegexTimeout);

    // Applied in this order (operations.md 6.5 step 1).
    private static readonly (Regex Regex, string Replacement)[] Normalisers =
    [
        (R(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b"), "<guid>"),
        (R(@"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}(?::\d{2}(?:[.,]\d+)?)?(?:Z|[+-]\d{2}:?\d{2})?"), "<ts>"),
        (R(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"), "<email>"),
        (R(@"\b[A-Za-z][A-Za-z0-9+.-]*://[^\s""'<>]+"), "<url>"),
        (R(@"\b(?:\d{1,3}\.){3}\d{1,3}\b"), "<ip>"),
        (R(@"(?<![\w:])(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}(?![\w:])|(?<![\w:])(?:[0-9a-fA-F]{1,4}:){1,7}(?::[0-9a-fA-F]{1,4}){1,7}(?![\w:])|(?<![\w:])(?:[0-9a-fA-F]{1,4}:){1,7}:(?![\w:])|(?<![\w:])::(?:[0-9a-fA-F]{1,4}:){0,6}[0-9a-fA-F]{1,4}(?![\w:])"), "<ip>"),
        (R(@"\b[0-9a-fA-F]{8,}\b"), "<hex>"),
        (R(@"""[^""\r\n]*""|(?<!\w)'[^'\r\n]*'(?!\w)"), "<str>"),
        (R(@"\b\d+(?:\.\d+)?\b"), "<num>"),
    ];

    private static readonly Regex Whitespace = R(@"\s+");

    public static string Normalise(string text)
    {
        foreach (var (regex, replacement) in Normalisers)
        {
            try { text = regex.Replace(text, replacement); }
            catch (RegexMatchTimeoutException) { /* keep the text as it is: the id stays deterministic for the same input */ }
        }
        text = Whitespace.Replace(text, " ").Trim();
        return text.Length > MaxTemplateLength ? text[..MaxTemplateLength] : text;
    }

    public static string Discriminator(string? eventName, string? messageTemplate, string message, Func<string, string?> property)
    {
        if (eventName is not null && DiscriminatorProperties.TryGetValue(eventName, out var names))
            return string.Join('|', names.Select(n => property(n) ?? ""));
        return Normalise(string.IsNullOrEmpty(messageTemplate) ? message : messageTemplate);
    }

    public static string Canonical(string? eventName, string category, string? exceptionType, string? topFrame, string discriminator) =>
        $"{eventName ?? ""}\n{category}\n{exceptionType ?? ""}\n{topFrame ?? ""}\n{discriminator}";

    public static string ComputeId(string canonical) =>
        "t-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant()[..12];

    public static string ComputeId(string? eventName, string category, string? exceptionType, string? topFrame, string discriminator) =>
        ComputeId(Canonical(eventName, category, exceptionType, topFrame, discriminator));

    public static TemplateInfo Extract(LogRecord record)
    {
        var eventName = record.EventName;
        var discriminator = Discriminator(eventName, record.MessageTemplate, record.Message, record.Property);
        var canonical = Canonical(eventName, record.Category, record.ExceptionType, record.TopFrame, discriminator);
        return new TemplateInfo(eventName, record.Category, record.ExceptionType, record.TopFrame, discriminator, canonical, ComputeId(canonical));
    }
}
