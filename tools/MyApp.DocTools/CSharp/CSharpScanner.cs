using System.Text;
using System.Text.RegularExpressions;

namespace MyApp.DocTools.CSharp;

public sealed record CsAttribute(string Name, IReadOnlyList<string> StringArgs);

public sealed class CsClass
{
    public string Name { get; init; } = "";
    public int Line { get; init; }
    public bool IsAbstract { get; init; }
    public bool IsStatic { get; init; }
    public List<CsAttribute> ClassAttributes { get; init; } = new();
    /// <summary>Attribute lists found anywhere inside the class body (methods, properties, nested types).</summary>
    public List<CsAttribute> BodyAttributes { get; init; } = new();
    /// <summary>Class body with comments blanked and string contents replaced by 'x'.</summary>
    public string MaskedBody { get; init; } = "";

    public IEnumerable<string> PropertyValues(string propertyName, IEnumerable<CsAttribute> attrs) =>
        attrs.Where(a => a.Name == "Property" && a.StringArgs.Count >= 2 && a.StringArgs[0] == propertyName)
             .Select(a => a.StringArgs[1]);

    public List<string> UserStoryTags => PropertyValues("UserStory", ClassAttributes).ToList();
    public List<string> TestCaseTags => PropertyValues("TestCase", ClassAttributes).ToList();
    public List<string> Categories =>
        ClassAttributes.Where(a => a.Name == "Category" && a.StringArgs.Count >= 1).Select(a => a.StringArgs[0]).ToList();

    /// <summary>UserStory and TestCase properties written on methods or other members, which section 11.1 forbids.</summary>
    public List<string> MethodTags => BodyAttributes
        .Where(a => a.Name == "Property" && a.StringArgs.Count >= 1 && a.StringArgs[0] is "UserStory" or "TestCase")
        .Select(a => $"Property({a.StringArgs[0]})")
        .ToList();

    /// <summary>Categories written on methods or other members; only one that names an area folder is a tag (section 11.1).</summary>
    public List<string> MethodCategories => BodyAttributes
        .Where(a => a.Name == "Category" && a.StringArgs.Count >= 1)
        .Select(a => a.StringArgs[0])
        .ToList();

    public bool IsTestClass =>
        !IsAbstract && !IsStatic &&
        (ClassAttributes.Any(a => a.Name == "TestFixture") ||
         BodyAttributes.Any(a => a.Name is "Test" or "TestCase" or "TestCaseSource" or "Theory" or "Combinatorial" or "Sequential"));
}

public sealed class CsFile
{
    public string Path { get; init; } = "";
    public List<CsClass> Classes { get; init; } = new();
    /// <summary>File text with comments blanked, strings intact.</summary>
    public string CodeText { get; init; } = "";
    /// <summary>File text with comments blanked and string contents replaced by 'x'.</summary>
    public string MaskedText { get; init; } = "";
}

/// <summary>Text scanning of C# sources: enough to read class-level attributes and class bodies. No compiler involved.</summary>
public static class CSharpScanner
{
    private static readonly HashSet<string> Modifiers = new()
    {
        "public", "internal", "private", "protected", "sealed", "abstract", "static", "partial", "unsafe", "file", "new",
    };

    private static readonly Regex ClassRx = new(@"\bclass\s+([A-Za-z_]\w*)", RegexOptions.Compiled);
    private static readonly Regex AttrItemRx = new(@"^\s*(?:\w+\s*:\s*)?([\w.]+)\s*(?:\((.*)\))?\s*$", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex StringArgRx = new("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

    /// <summary>Returns the text with comments blanked and, in the second value, string contents also blanked.</summary>
    public static (string Code, string Masked) Mask(string text)
    {
        var code = text.ToCharArray();
        var masked = text.ToCharArray();
        var n = text.Length;

        void Blank(char[] arr, int from, int to, char with)
        {
            for (var k = from; k < to && k < n; k++) if (arr[k] != '\n' && arr[k] != '\r') arr[k] = with;
        }

        var i = 0;
        while (i < n)
        {
            var c = text[i];
            if (c == '/' && i + 1 < n && text[i + 1] == '/')
            {
                var e = text.IndexOf('\n', i);
                if (e < 0) e = n;
                Blank(code, i, e, ' '); Blank(masked, i, e, ' ');
                i = e;
            }
            else if (c == '/' && i + 1 < n && text[i + 1] == '*')
            {
                var e = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                e = e < 0 ? n : e + 2;
                Blank(code, i, e, ' '); Blank(masked, i, e, ' ');
                i = e;
            }
            else if (c == '"')
            {
                // raw string: three or more quotes
                var q = 0;
                while (i + q < n && text[i + q] == '"') q++;
                var verbatim = i > 0 && (text[i - 1] == '@' || (i > 1 && text[i - 1] == '$' && text[i - 2] == '@'));
                if (q >= 3 && !verbatim)
                {
                    var closer = new string('"', q);
                    var e = text.IndexOf(closer, i + q, StringComparison.Ordinal);
                    e = e < 0 ? n : e;
                    Blank(masked, i + q, e, 'x');
                    i = Math.Min(n, e + q);
                }
                else if (verbatim)
                {
                    var j = i + 1;
                    while (j < n)
                    {
                        if (text[j] == '"') { if (j + 1 < n && text[j + 1] == '"') { j += 2; continue; } break; }
                        j++;
                    }
                    Blank(masked, i + 1, j, 'x');
                    i = Math.Min(n, j + 1);
                }
                else
                {
                    var j = i + 1;
                    while (j < n && text[j] != '"' && text[j] != '\n')
                    {
                        if (text[j] == '\\') j++;
                        j++;
                    }
                    Blank(masked, i + 1, Math.Min(j, n), 'x');
                    i = Math.Min(n, j + 1);
                }
            }
            else if (c == '\'')
            {
                var j = i + 1;
                if (j < n && text[j] == '\\') j += 2; else j++;
                while (j < n && text[j] != '\'' && text[j] != '\n') j++;
                Blank(masked, i + 1, Math.Min(j, n), 'x');
                i = Math.Min(n, j + 1);
            }
            else i++;
        }
        return (new string(code), new string(masked));
    }

    public static CsFile Parse(string path, string text)
    {
        var (code, masked) = Mask(text);
        var file = new CsFile { Path = path, CodeText = code, MaskedText = masked };
        foreach (Match m in ClassRx.Matches(masked))
        {
            var cls = ParseClass(code, masked, m);
            if (cls is not null) file.Classes.Add(cls);
        }
        return file;
    }

    private static CsClass? ParseClass(string code, string masked, Match m)
    {
        // walk back over modifiers and attribute lists
        var pos = m.Index;
        var mods = new HashSet<string>();
        var attrs = new List<CsAttribute>();
        while (true)
        {
            var q = pos - 1;
            while (q >= 0 && char.IsWhiteSpace(masked[q])) q--;
            if (q < 0) break;
            if (masked[q] == ']')
            {
                var depth = 0; var s = q;
                for (; s >= 0; s--)
                {
                    if (masked[s] == ']') depth++;
                    else if (masked[s] == '[') { depth--; if (depth == 0) break; }
                }
                if (s < 0) break;
                attrs.InsertRange(0, ParseAttributeList(code, masked, s, q));
                pos = s;
                continue;
            }
            var e = q;
            while (q >= 0 && (char.IsLetterOrDigit(masked[q]) || masked[q] == '_')) q--;
            if (q == e) break;
            var word = masked[(q + 1)..(e + 1)];
            if (!Modifiers.Contains(word)) break;
            mods.Add(word);
            pos = q + 1;
        }

        // find the body
        var open = -1;
        for (var k = m.Index + m.Length; k < masked.Length; k++)
        {
            if (masked[k] == '{') { open = k; break; }
            if (masked[k] == ';') break;
        }
        var body = "";
        var bodyAttrs = new List<CsAttribute>();
        if (open >= 0)
        {
            var depth = 0; var close = masked.Length - 1;
            for (var k = open; k < masked.Length; k++)
            {
                if (masked[k] == '{') depth++;
                else if (masked[k] == '}') { depth--; if (depth == 0) { close = k; break; } }
            }
            body = masked[(open + 1)..close];
            bodyAttrs = FindBodyAttributes(code, masked, open + 1, close);
        }
        var line = 1 + masked.Take(m.Index).Count(ch => ch == '\n');
        return new CsClass
        {
            Name = m.Groups[1].Value,
            Line = line,
            IsAbstract = mods.Contains("abstract"),
            IsStatic = mods.Contains("static"),
            ClassAttributes = attrs,
            BodyAttributes = bodyAttrs,
            MaskedBody = body,
        };
    }

    private static List<CsAttribute> FindBodyAttributes(string code, string masked, int from, int to)
    {
        var result = new List<CsAttribute>();
        for (var k = from; k < to; k++)
        {
            if (masked[k] != '[') continue;
            var p = k - 1;
            while (p >= from && char.IsWhiteSpace(masked[p])) p--;
            var prev = p < from ? '\0' : masked[p];
            if (!(prev == '\0' || prev == ';' || prev == '{' || prev == '}' || prev == ']')) continue;
            var depth = 0; var e = -1;
            for (var s = k; s < to; s++)
            {
                if (masked[s] == '[') depth++;
                else if (masked[s] == ']') { depth--; if (depth == 0) { e = s; break; } }
            }
            if (e < 0) continue;
            result.AddRange(ParseAttributeList(code, masked, k, e));
        }
        return result;
    }

    private static List<CsAttribute> ParseAttributeList(string code, string masked, int open, int close)
    {
        var result = new List<CsAttribute>();
        var start = open + 1;
        var depth = 0;
        for (var k = open + 1; k <= close; k++)
        {
            var ch = masked[k];
            if (ch == '(' || ch == '[' || ch == '{') depth++;
            else if (ch == ')' || ch == '}' || (ch == ']' && k != close)) depth--;
            if ((ch == ',' && depth == 0) || k == close)
            {
                var item = code[start..k];
                var am = AttrItemRx.Match(item);
                if (am.Success)
                {
                    var name = am.Groups[1].Value;
                    name = name[(name.LastIndexOf('.') + 1)..];
                    if (name.EndsWith("Attribute", StringComparison.Ordinal) && name.Length > 9) name = name[..^9];
                    var args = new List<string>();
                    if (am.Groups[2].Success)
                        foreach (Match sm in StringArgRx.Matches(am.Groups[2].Value)) args.Add(sm.Groups[1].Value);
                    result.Add(new CsAttribute(name, args));
                }
                start = k + 1;
            }
        }
        return result;
    }
}
