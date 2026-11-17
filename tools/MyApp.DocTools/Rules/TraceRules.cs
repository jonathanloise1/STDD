using System.Text.RegularExpressions;
using MyApp.DocTools.CSharp;
using MyApp.DocTools.Model;

namespace MyApp.DocTools.Rules;

/// <summary>R13: Observable trace present iff Edge case or Error handling, and valid.</summary>
public sealed class R13ObservableTraceComplete : Rule
{
    public override string Id => "R13";
    public override string Name => "Observable trace complete";

    private static readonly Regex RunbookRx = new(@"^\[([^\]]+)\]\(([^)]+)\)$", RegexOptions.Compiled);

    public override IEnumerable<Finding> Check(Repo repo)
    {
        var reg = repo.Registries;
        var byKey = new Dictionary<string, List<string>>();
        foreach (var t in repo.AllTestCases)
            if (t.LogKey is { } k) { if (!byKey.TryGetValue(k, out var l)) byKey[k] = l = new(); l.Add(t.Path); }

        foreach (var t in repo.AllTestCases)
        {
            if (t.NeedsTrace && !t.HasTraceSection) { yield return Found(t.Path, $"Type is {t.Type}, which requires an \"## Observable trace\" section."); continue; }
            if (!t.NeedsTrace && t.HasTraceSection) { yield return Found(t.Path, $"Type is {t.Type}; only Edge case and Error handling have an Observable trace. Remove it or change the type."); continue; }
            if (!t.HasTraceSection) continue;

            var table = t.SectionTable(Templates.TraceH2);
            if (table is null) { yield return Found(t.Path, "The Observable trace has no table."); continue; }
            if (!table.Header.SequenceEqual(new[] { "Field", "Value" })) yield return Found(t.Path, "The Observable trace table must have the columns Field | Value.");
            var fields = t.TraceRows().Select(r => r.Field).ToList();
            if (!fields.SequenceEqual(new[] { "Message", "Log key", "Severity", "Runbook" }))
                yield return Found(t.Path, $"The Observable trace must have exactly the fields Message, Log key, Severity, Runbook in this order; found {string.Join(", ", fields)}.");

            if (t.TraceValue("Message") is { } msg)
            {
                var ok = Regex.IsMatch(msg, "^\".+\"$") || (msg == "None" && t.Type == "Edge case");
                if (!ok) yield return Found(t.Path, "Trace Message must be the exact English message in double quotes (\"None\" is allowed only for an Edge case where the user sees no message).");
            }

            var key = t.LogKey;
            if (t.TraceValue("Log key") is { } rawKey && key is null)
                yield return Found(t.Path, $"Trace Log key must be written in backticks; found {rawKey}.");
            if (key is not null)
            {
                if (!Ids.LogKey.IsMatch(key)) yield return Found(t.Path, $"Log key \"{key}\" does not match {{area}}.{{action}}.{{outcome}} in lower case with the outcome in snake case.");
                else
                {
                    var p = key.Split('.');
                    if (p[2].Split('_').Length > 4) yield return Found(t.Path, $"The outcome of log key \"{key}\" has more than 4 words.");
                    if (t.IdParts() is { } id)
                    {
                        if (p[0] != id.Area.ToLowerInvariant()) yield return Found(t.Path, $"Log key area \"{p[0]}\" must be the area code of the test case in lower case (\"{id.Area.ToLowerInvariant()}\").");
                        if (p[1] != id.Action.ToLowerInvariant()) yield return Found(t.Path, $"Log key action \"{p[1]}\" must be the action of the test case in lower case (\"{id.Action.ToLowerInvariant()}\").");
                    }
                }
                if (byKey[key].Count > 1)
                    yield return Found(t.Path, $"Log key \"{key}\" is also defined by {string.Join(", ", byKey[key].Where(x => x != t.Path))}; a log key is defined by one test case.");
            }

            if (t.TraceValue("Severity") is { } sev && !reg.Values("Severity").Contains(sev))
                yield return Found(t.Path, $"Trace Severity \"{sev}\" is not one of {string.Join(", ", reg.Values("Severity"))}.");

            if (t.TraceValue("Runbook") is { } rb)
            {
                var m = RunbookRx.Match(rb);
                if (!m.Success) yield return Found(t.Path, "Trace Runbook must be a link [anchor](../operations.md#anchor).");
                else if (key is not null && Ids.LogKey.IsMatch(key))
                {
                    var anchor = Ids.RunbookAnchor(key);
                    if (m.Groups[1].Value != anchor) yield return Found(t.Path, $"Runbook link text must be \"{anchor}\".");
                    if (m.Groups[2].Value != $"../operations.md#{anchor}") yield return Found(t.Path, $"Runbook link target must be \"../operations.md#{anchor}\".");
                }
            }
        }
    }
}

/// <summary>R14: the area's operations.md exists and has an H2 whose text is the runbook anchor.</summary>
public sealed class R14RunbookExists : Rule
{
    public override string Id => "R14";
    public override string Name => "Runbook exists";

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var area in repo.Areas)
            foreach (var t in area.TestCases)
            {
                if (t.LogKey is not { } key || !Ids.LogKey.IsMatch(key)) continue;
                var anchor = Ids.RunbookAnchor(key);
                if (area.Operations is null)
                    yield return Found(t.Path, $"{area.Path}/operations.md does not exist; it must hold a runbook heading for \"{anchor}\".");
                else if (!area.Operations.Doc.Headings.Any(h => h.Level == 2 && h.Text == anchor))
                    yield return Found(t.Path, $"{area.Path}/operations.md has no heading \"## {anchor}\". Add the runbook for log key {key}; the heading is the anchor itself, not the log key (section 3.1).");
            }
    }
}

/// <summary>R15: the key is one constant of the area's log class, that constant names a LoggerMessage method, and the method has one call site.</summary>
public sealed class R15LogKeyEmitted : Rule
{
    public override string Id => "R15";
    public override string Name => "Log key emitted";

    public override IEnumerable<Finding> Check(Repo repo)
    {
        foreach (var t in repo.AllTestCases)
        {
            if (t.LogKey is not { } key) continue;
            foreach (var m in Verify(repo, t, key)) yield return Found(t.Path, m);
        }
    }

    private static IEnumerable<string> Verify(Repo repo, TestCaseFile t, string key)
    {
        var needle = "\"" + key + "\"";
        var places = new List<(CsFile File, int Line)>();
        foreach (var f in repo.BackendSources)
            for (var i = f.CodeText.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = f.CodeText.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
                places.Add((f, 1 + f.CodeText.Take(i).Count(c => c == '\n')));

        if (places.Count == 0) { yield return $"The log key \"{key}\" does not appear as a string literal under src/backend/. Add its log definition (operations.md 3.1) and its call site."; yield break; }
        if (places.Count > 1) { yield return $"The log key \"{key}\" appears at {places.Count} places ({string.Join(", ", places.Select(p => $"{p.File.Path}:{p.Line}"))}); it must appear at exactly one, the constant of the log class. Elsewhere use the constant."; yield break; }

        var file = places[0].File;
        var expected = $"src/backend/MyApp.Application/Logging/{Ids.AreaPascal(t.Area.Folder)}Log.cs";
        var byTitle = t.Area.Registry is { } row ? $"src/backend/MyApp.Application/Logging/{Ids.TitlePascal(row.Title)}Log.cs" : expected;
        if (file.Path != expected && file.Path != byTitle) yield return $"The log key \"{key}\" is at {file.Path}:{places[0].Line}; it belongs in {expected} (operations.md 3.1).";

        var constant = Regex.Match(file.CodeText, $@"\bconst\s+string\s+(\w+)\s*=\s*{Regex.Escape(needle)}");
        if (!constant.Success) { yield return $"The log key \"{key}\" must be a constant: public const string {{MethodName}} = \"{key}\"; in the nested Keys class."; yield break; }
        var name = constant.Groups[1].Value;

        var method = Regex.Match(file.MaskedText, $@"\bpartial\s+void\s+{name}\s*\(");
        if (!method.Success) { yield return $"The log class has no method \"partial void {name}(...)\" for the log key \"{key}\"."; yield break; }
        var declarationStart = file.MaskedText.LastIndexOfAny(new[] { ';', '{', '}' }, method.Index) + 1;
        var declaration = file.MaskedText[declarationStart..method.Index];
        if (!declaration.Contains("LoggerMessage", StringComparison.Ordinal) || !Regex.IsMatch(declaration, $@"\bEventName\s*=\s*Keys\.{name}\b"))
            yield return $"The method {name} must have [LoggerMessage(..., EventName = Keys.{name}, ...)] (operations.md 3.1).";

        var calls = repo.BackendSources.Sum(f => Regex.Matches(f.MaskedText, $@"\.\s*{name}\s*\(").Count);
        if (declaration.Contains("ClientReported", StringComparison.Ordinal))
        {
            if (calls > 0) yield return $"The method {name} is marked [ClientReported], so the dispatcher is its call site (operations.md 3.6); it is also called at {calls} place(s) under src/backend/.";
        }
        else if (calls == 0) yield return $"The method {name} of the log key \"{key}\" is never called under src/backend/. Add the call site where the situation is detected, or mark it [ClientReported] when only the browser sees it (operations.md 3.6).";
        else if (calls > 1) yield return $"The method {name} of the log key \"{key}\" is called at {calls} places under src/backend/; a log key has one log call site (section 3.1).";
    }
}
