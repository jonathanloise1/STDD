using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MyApp.Infrastructure.Logging;

/// <summary>
/// The template parameters <c>ExceptionType</c> and <c>TopFrame</c> of platform events (operations.md 3.5).
/// </summary>
public readonly record struct ExceptionSummary(string ExceptionType, string TopFrame)
{
    private const string AppNamespacePrefix = "MyApp.";
    public const string UnknownFrame = "unknown";

    private static readonly Regex StateMachineType = new(@"^<(?<m>.+)>d__\d+$", RegexOptions.Compiled);
    private static readonly Regex LambdaMethod = new(@"^<(?<m>[^>]+)>b__", RegexOptions.Compiled);
    private static readonly Regex LocalFunction = new(@"^<[^>]+>g__(?<m>[^|]+)\|", RegexOptions.Compiled);

    public static ExceptionSummary From(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var innermost = exception;
        while (innermost.InnerException is not null)
            innermost = innermost.InnerException;

        return new ExceptionSummary(innermost.GetType().FullName ?? innermost.GetType().Name, FindTopFrame(exception));
    }

    private static string FindTopFrame(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            foreach (var frame in new StackTrace(current, false).GetFrames())
            {
                var method = frame.GetMethod();
                if (method?.DeclaringType?.Namespace is { } ns
                    && (ns.StartsWith(AppNamespacePrefix, StringComparison.Ordinal) || ns == AppNamespacePrefix.TrimEnd('.')))
                {
                    return FormatFrame(method);
                }
            }
        }

        return UnknownFrame;
    }

    /// <summary>Formats a frame as <c>Namespace.Type.Method</c> with compiler-generated names reduced to the source method.</summary>
    public static string FormatFrame(MethodBase method)
    {
        var type = method.DeclaringType;
        var name = method.Name;

        if (type is not null && StateMachineType.Match(type.Name) is { Success: true } sm)
        {
            name = sm.Groups["m"].Value;
            type = type.DeclaringType;
        }
        else if (LambdaMethod.Match(name) is { Success: true } lm)
        {
            name = lm.Groups["m"].Value;
        }
        else if (LocalFunction.Match(name) is { Success: true } lf)
        {
            name = lf.Groups["m"].Value;
        }

        // Closure classes (<>c, <>c__DisplayClass0_0) are compiler-generated: use the enclosing type.
        while (type is not null && type.Name.StartsWith("<>", StringComparison.Ordinal))
            type = type.DeclaringType;

        var typeName = type?.FullName?.Replace('+', '.') ?? "unknown";
        return $"{typeName}.{name}";
    }
}
