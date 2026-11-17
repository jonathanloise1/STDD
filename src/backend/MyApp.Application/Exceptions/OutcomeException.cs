namespace MyApp.Application.Exceptions;

/// <summary>
/// A request that cannot complete for a situation that has a log key (operations.md 4.2).
/// The key is always a constant of a log definition class (for example <c>TasksLog.Keys.X</c>), never a literal.
/// The keyed log entry is written where the situation is detected; the exception handling middleware
/// writes none and returns the key as the <c>code</c> of the problem document.
/// </summary>
public class OutcomeException : Exception
{
    /// <param name="key">The log key constant.</param>
    /// <param name="statusCode">The HTTP status of the response.</param>
    /// <param name="detail">The English user message (the Observable trace Message).</param>
    /// <param name="errors">Field name to messages, for validation outcomes only.</param>
    public OutcomeException(string key, int statusCode, string detail, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(detail)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        StatusCode = statusCode;
        Errors = errors;
    }

    public string Key { get; }

    public int StatusCode { get; }

    /// <summary>The user message; equal to <see cref="Exception.Message"/>.</summary>
    public string Detail => Message;

    /// <summary>Field errors (validation only), keyed by camelCase field name.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }
}
