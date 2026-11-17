using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MyApp.Monitor.Pack;

/// <summary>A source-generated LoggerMessage method (operations.md 3.1).</summary>
public sealed record LogDefinition(
    string File, int Line, string Class, string Method, string? EventName, string? Level, string? Template,
    IReadOnlyList<string> Parameters, string? StoryComment);

/// <summary>Syntax-only Roslyn scan of <c>src/backend/**/*.cs</c> for LoggerMessage definitions (operations.md 5.2 step 2).</summary>
public static class CallSiteScanner
{
    private static readonly Regex Placeholder = new(@"\{[@$]?(?<name>[A-Za-z_][A-Za-z0-9_]*)(?:[,:][^}]*)?\}", RegexOptions.CultureInvariant);

    /// <summary>The source files of <c>src/backend/</c> (build output left out), with their path relative to the repository root.</summary>
    private static IEnumerable<(string File, string Relative)> BackendFiles(string repoRoot)
    {
        var backend = Path.Combine(repoRoot, "src", "backend");
        if (!Directory.Exists(backend)) yield break;
        foreach (var file in Directory.EnumerateFiles(backend, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !IsBuildOutput(Path.GetRelativePath(backend, f)))
                     .OrderBy(f => f, StringComparer.Ordinal))
            yield return (file, Path.GetRelativePath(repoRoot, file).Replace('\\', '/'));
    }

    public static List<LogDefinition> ScanRepository(string repoRoot)
    {
        var result = new List<LogDefinition>();
        foreach (var (file, rel) in BackendFiles(repoRoot))
            result.AddRange(ScanText(File.ReadAllText(file), rel));
        return result;
    }

    // ---- operations.md 16: checks O02 and O03 ----

    /// <summary>Placeholder names that carry personal data (O03). Compared without regard to case.</summary>
    public static readonly IReadOnlySet<string> PersonalPlaceholders = new HashSet<string>(
        ["Email", "FirstName", "LastName", "FullName", "Name", "Phone", "PhoneNumber", "Address", "Title", "Description", "Password", "Token"],
        StringComparer.OrdinalIgnoreCase);

    private const string LoggingFolder = "/MyApp.Application/Logging/";

    /// <summary>
    /// O02 and O03 over <c>src/backend/</c>. Warning, Error and Critical are written only by log definitions (O02), so a direct call outside
    /// <c>MyApp.Application/Logging/</c> is a finding. No log message template names a placeholder of <see cref="PersonalPlaceholders"/> (O03): the
    /// templates checked are the <c>Message</c> of every <c>LoggerMessage</c> definition (already scanned: <paramref name="definitions"/>) and the string
    /// literals passed to any <c>Log*</c> call or <c>BeginScope</c>, in any folder.
    /// </summary>
    public static List<Finding> ScanPolicy(string repoRoot, IReadOnlyList<LogDefinition> definitions)
    {
        var result = new List<Finding>();
        foreach (var (file, rel) in BackendFiles(repoRoot))
            result.AddRange(ScanPolicyText(File.ReadAllText(file), rel));
        foreach (var d in definitions)
            foreach (var name in d.Parameters.Where(PersonalPlaceholders.Contains))
                result.Add(new Finding("O03", $"the template of '{d.Method}' has the placeholder '{{{name}}}', which names personal data", $"{d.File}:{d.Line}"));
        return result;
    }

    /// <summary>O02 and O03 for the log calls of one source file (the definitions are checked by <see cref="ScanPolicy"/>).</summary>
    public static List<Finding> ScanPolicyText(string source, string relativePath)
    {
        var result = new List<Finding>();
        var inLogging = ("/" + relativePath.Replace('\\', '/')).Contains(LoggingFolder, StringComparison.Ordinal);
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = CalleeName(call.Expression);
            if (name is null) continue;
            var line = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var location = $"{relativePath}:{line}";
            var args = call.ArgumentList.Arguments;
            if (!inLogging && (name is "LogWarning" or "LogError" or "LogCritical"
                               || name == "Log" && args.Count > 0 && LastIdentifier(args[0].Expression) is "Warning" or "Error" or "Critical" && args[0].Expression.ToString().Contains("LogLevel", StringComparison.Ordinal)))
                result.Add(new Finding("O02", "Warning, Error and Critical are logged only by a definition in MyApp.Application/Logging/, not by a direct call", location));
            if (name.StartsWith("Log", StringComparison.Ordinal) || name == "BeginScope")
                foreach (var arg in args)
                    if (arg.Expression is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
                        foreach (Match m in Placeholder.Matches(lit.Token.ValueText))
                            if (PersonalPlaceholders.Contains(m.Groups["name"].Value))
                                result.Add(new Finding("O03", $"the template has the placeholder '{{{m.Groups["name"].Value}}}', which names personal data", location));
        }
        return result;
    }

    private static string? CalleeName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax m => SimpleName(m.Name),
        SimpleNameSyntax s => SimpleName(s),
        MemberBindingExpressionSyntax b => SimpleName(b.Name),
        _ => null,
    };

    private static string SimpleName(SimpleNameSyntax name) => name.Identifier.Text;

    private static bool IsBuildOutput(string relativeToBackend) =>
        relativeToBackend.Replace('\\', '/').Split('/').Any(s => s is "bin" or "obj");

    public static List<LogDefinition> ScanText(string source, string relativePath)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        var keysByClass = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var allKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(c => c.Identifier.Text == "Keys"))
        {
            var owner = cls.Parent is ClassDeclarationSyntax o ? o.Identifier.Text : "";
            if (!keysByClass.TryGetValue(owner, out var map)) keysByClass[owner] = map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in cls.Members.OfType<FieldDeclarationSyntax>().Where(f => f.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword))))
                foreach (var variable in field.Declaration.Variables)
                    if (variable.Initializer?.Value is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        map[variable.Identifier.Text] = lit.Token.ValueText;
                        allKeys[variable.Identifier.Text] = lit.Token.ValueText;
                    }
        }

        var result = new List<LogDefinition>();
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var attribute = method.AttributeLists.SelectMany(l => l.Attributes)
                .FirstOrDefault(a => a.Name.ToString() is "LoggerMessage" or "LoggerMessageAttribute" or "Microsoft.Extensions.Logging.LoggerMessage");
            if (attribute is null) continue;
            var owner = method.Parent is ClassDeclarationSyntax c ? c.Identifier.Text : "";
            keysByClass.TryGetValue(owner, out var ownerKeys);

            string? eventName = null, level = null, template = null;
            foreach (var arg in attribute.ArgumentList?.Arguments ?? [])
            {
                var name = arg.NameEquals?.Name.Identifier.Text;
                switch (name)
                {
                    case "EventName": eventName = ResolveString(arg.Expression, ownerKeys, allKeys); break;
                    case "Level": level = LastIdentifier(arg.Expression); break;
                    case "Message": template = ResolveString(arg.Expression, ownerKeys, allKeys); break;
                }
            }

            var parameters = template is null ? [] : Placeholder.Matches(template).Select(m => m.Groups["name"].Value).Distinct().ToList();
            var location = method.GetFirstToken().GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            result.Add(new LogDefinition(relativePath, location, owner, method.Identifier.Text, eventName, level, template, parameters, StoryComment(method)));
        }
        return result;
    }

    private static string? StoryComment(MethodDeclarationSyntax method)
    {
        var trivia = method.GetLeadingTrivia();
        // The comment must be the line immediately above the attribute: only whitespace or one line break may follow it.
        for (var i = trivia.Count - 1; i >= 0; i--)
        {
            var t = trivia[i];
            if (t.IsKind(SyntaxKind.SingleLineCommentTrivia)) return t.ToString().TrimStart('/').Trim();
            if (t.IsKind(SyntaxKind.WhitespaceTrivia)) continue;
            if (t.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                // allow exactly the end of the comment's own line
                if (i > 0 && trivia[i - 1].IsKind(SyntaxKind.SingleLineCommentTrivia)) continue;
                if (i > 1 && trivia[i - 1].IsKind(SyntaxKind.WhitespaceTrivia) && trivia[i - 2].IsKind(SyntaxKind.SingleLineCommentTrivia)) continue;
                return null;
            }
            return null;
        }
        return null;
    }

    private static string? LastIdentifier(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
        IdentifierNameSyntax i => i.Identifier.Text,
        _ => null,
    };

    private static string? ResolveString(ExpressionSyntax expression, Dictionary<string, string>? ownerKeys, Dictionary<string, string> allKeys)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                return lit.Token.ValueText;
            case BinaryExpressionSyntax bin when bin.IsKind(SyntaxKind.AddExpression):
            {
                var left = ResolveString(bin.Left, ownerKeys, allKeys);
                var right = ResolveString(bin.Right, ownerKeys, allKeys);
                return left is null || right is null ? null : left + right;
            }
            case ParenthesizedExpressionSyntax p:
                return ResolveString(p.Expression, ownerKeys, allKeys);
            case MemberAccessExpressionSyntax m:
            {
                var name = m.Name.Identifier.Text;
                if (ownerKeys is not null && ownerKeys.TryGetValue(name, out var v)) return v;
                return allKeys.TryGetValue(name, out var any) ? any : null;
            }
            case IdentifierNameSyntax id:
                if (ownerKeys is not null && ownerKeys.TryGetValue(id.Identifier.Text, out var own)) return own;
                return allKeys.TryGetValue(id.Identifier.Text, out var a) ? a : null;
            default:
                return null;
        }
    }
}
