using System.Text.Json;
using System.Text.RegularExpressions;
using MyApp.DocTools.Markdown;
using MyApp.DocTools.Model;

namespace MyApp.DocTools.Rules;

/// <summary>R22: story ids under src/ and tests/ name story files, except in test data; in src/ they appear only in the comment form, right above a declaration.</summary>
public sealed class R22StoryIdsInCode : Rule
{
    public override string Id => "R22";
    public override string Name => "Story ids in code";

    private const string Ids1 = @"US-[A-Z]{2,8}-\d{2}";
    private static readonly Regex CommentForm = new($@"^\s*(//\s*{Ids1}(\s*,\s*{Ids1})*|\{{/\*\s*{Ids1}(\s*,\s*{Ids1})*\s*\*/\}})\s*$", RegexOptions.Compiled);
    /// <summary>What cannot start a declaration: nothing, a comment, a closing token, an import, a statement or a plain call.</summary>
    private static readonly Regex NotADeclaration = new(@"^(?:$|//|/\*|\*|\{/\*|\{$|\}|\)|\]|</|/>|(?:using|namespace|import|var)\s|(?:return|if|else|for|foreach|while|switch|try|catch|throw|await)\b|(?:const|let)\s*[\[{]|\w+(?:\.\w+)+\s*\()", RegexOptions.Compiled);
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".tsx", ".js", ".jsx", ".mjs", ".html", ".css", ".scss", ".json", ".yml", ".yaml", ".sql", ".razor", ".cshtml",
    };

    public override IEnumerable<Finding> Check(Repo repo)
    {
        var stories = repo.AllStories.Select(s => s.Stem).ToHashSet();
        foreach (var file in repo.EnumerateFiles("src").Concat(repo.EnumerateFiles("tests")))
        {
            if (Repo.HoldsStoryIdsAsTestData(file)) continue;
            if (!Extensions.Contains(Path.GetExtension(file)) || file.EndsWith("package-lock.json", StringComparison.Ordinal)) continue;
            var info = new FileInfo(repo.Abs(file));
            if (info.Length > 2_000_000) continue;
            var inSrc = file.StartsWith("src/", StringComparison.Ordinal);
            var lines = File.ReadAllLines(repo.Abs(file));
            var seen = new HashSet<string>();
            for (var i = 0; i < lines.Length; i++)
            {
                var ms = Model.Ids.StoryInText.Matches(lines[i]);
                if (ms.Count == 0) continue;
                foreach (Match m in ms.Where(m => !stories.Contains(m.Value)))
                    if (seen.Add("u" + m.Value)) yield return Found(file, $"Line {i + 1}: the story id {m.Value} names no story file.");
                if (!inSrc) continue;
                if (!CommentForm.IsMatch(lines[i]))
                {
                    if (seen.Add("f" + i)) yield return Found(file, $"Line {i + 1}: a story id in src/ must be alone in a comment line: \"// US-AREA-NN\" or \"{{/* US-AREA-NN */}}\" (section 11.2).");
                }
                else if (i + 1 == lines.Length || NotADeclaration.IsMatch(lines[i + 1].Trim()))
                    yield return Found(file, $"Line {i + 1}: the story-id comment must be the line immediately above the declaration it marks (section 11.2); a blank line, comment, import or statement follows it.");
            }
        }
    }
}

/// <summary>R23: docs/contracts.json parses, lists exactly the formats of section 13 and resolves each spec anchor, in whichever guideline it points to.</summary>
public sealed class R23ContractVersions : Rule
{
    public override string Id => "R23";
    public override string Name => "Contract versions";

    private static JsonDocument? TryParse(string text, out string? error)
    {
        try { error = null; return JsonDocument.Parse(text); }
        catch (JsonException e) { error = e.Message; return null; }
    }

    public override IEnumerable<Finding> Check(Repo repo)
    {
        var path = Repo.ContractsPath;
        var text = repo.ReadText(path);
        if (text is null) { yield return Found(path, "The file is missing."); yield break; }
        var doc = TryParse(text, out var parseError);
        if (doc is null) { yield return Found(path, $"The file is not valid JSON: {parseError}"); yield break; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("contracts", out var contracts) || contracts.ValueKind != JsonValueKind.Object)
            { yield return Found(path, "The file needs a \"contracts\" object."); yield break; }
            foreach (var key in repo.Registries.ContractKeys.Where(k => !contracts.TryGetProperty(k, out _)))
                yield return Found(path, $"The format \"{key}\" of section 13 is not listed.");
            foreach (var entry in contracts.EnumerateObject().Where(e => repo.Registries.Loaded && !repo.Registries.ContractKeys.Contains(e.Name)))
                yield return Found(path, $"The format \"{entry.Name}\" is not in the table of section 13. Add it there or remove it here.");
            foreach (var entry in contracts.EnumerateObject())
            {
                var v = entry.Value;
                if (v.ValueKind != JsonValueKind.Object) { yield return Found(path, $"\"{entry.Name}\" must be an object with version and spec."); continue; }
                if (!v.TryGetProperty("version", out var ver) || ver.ValueKind != JsonValueKind.Number || !ver.TryGetInt32(out var n) || n < 1)
                    yield return Found(path, $"\"{entry.Name}\" needs an integer \"version\" of 1 or more.");
                if (!v.TryGetProperty("spec", out var spec) || spec.ValueKind != JsonValueKind.String) { yield return Found(path, $"\"{entry.Name}\" needs a \"spec\" string (file#anchor)."); continue; }
                var s = spec.GetString()!;
                var parts = s.Split('#', 2);
                var md = repo.LoadMd(parts[0]);
                if (md is null) { yield return Found(path, $"\"{entry.Name}\": the spec file \"{parts[0]}\" does not exist."); continue; }
                if (parts.Length == 2 && !md.Doc.Headings.Any(h => MarkdownDocument.Slug(h.Text) == parts[1]))
                    yield return Found(path, $"\"{entry.Name}\": the anchor \"#{parts[1]}\" matches no heading of {parts[0]}.");
            }
        }
    }
}
