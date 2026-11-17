using System.Text;
using System.Text.Json.Nodes;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Pack;

public sealed class GenerationResult
{
    public SignatureTable Table { get; } = new();
    /// <summary>Runbook sections by anchor or template id, as written to <c>runbooks/{id}.md</c>.</summary>
    public SortedDictionary<string, string> RunbookSections { get; } = new(StringComparer.Ordinal);
    public FindingList Findings { get; } = [];
}

/// <summary>Signature generation (operations.md 5.2): test cases + log definitions + runbooks -> signatures.json.</summary>
public static class SignatureGenerator
{
    public const string UnkeyedRunbookPath = "docs/support/unkeyed-runbooks.md";

    public static GenerationResult Generate(string repoRoot, DateOnly today, IReadOnlySet<string>? knownTools = null, JsonNode? signatureSchema = null)
    {
        var result = new GenerationResult();
        var findings = result.Findings;

        // 1. test cases with an Observable trace
        var all = TestCaseParser.ReadAll(repoRoot, findings);
        var traced = all.Where(t => t.Trace is not null && t.Type is "Edge case" or "Error handling").ToList();
        CheckTraces(traced, findings);

        // 2. log definitions
        var definitions = CallSiteScanner.ScanRepository(repoRoot);
        foreach (var violation in CallSiteScanner.ScanPolicy(repoRoot, definitions)) findings.Add(violation);   // O02, O03
        var byKey = definitions.Where(d => d.EventName is not null).GroupBy(d => d.EventName!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        // 4. area runbooks
        var options = new RunbookParser.Options(today, knownTools);
        var areaRunbooks = new Dictionary<string, RunbookDocument>(StringComparer.Ordinal);
        var areas = traced.Select(t => t.Area).Concat(AreasWithRunbook(repoRoot)).Distinct(StringComparer.Ordinal).OrderBy(a => a, StringComparer.Ordinal);
        foreach (var area in areas)
        {
            var rel = $"docs/areas/{area}/operations.md";
            var abs = Path.Combine(repoRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(abs))
            {
                if (traced.Any(t => t.Area == area)) findings.Add("R14", $"area '{area}' has Observable traces but no operations.md", rel);
                continue;
            }
            areaRunbooks[area] = RunbookParser.ParseArea(File.ReadAllText(abs), rel, options, findings);
        }

        // 3 + 6. join
        var signatures = new List<Signature>();
        foreach (var tc in traced.OrderBy(t => t.Trace!.LogKey, StringComparer.Ordinal))
        {
            var trace = tc.Trace!;
            var key = trace.LogKey;
            if (!byKey.TryGetValue(key, out var defs))
            {
                findings.Add("R15", $"log key '{key}' of {tc.Id} has no log definition under src/backend", tc.Path);
                continue;
            }
            if (defs.Count > 1)
                findings.Add("R15", $"log key '{key}' is defined {defs.Count} times: {string.Join(", ", defs.Select(d => $"{d.File}:{d.Line}"))}", tc.Path);
            var def = defs[0];

            var ok = true;
            if (def.Level is null || !string.Equals(def.Level, trace.Severity, StringComparison.Ordinal))
            {
                findings.Add("O04", $"definition level '{def.Level ?? "?"}' differs from the Severity '{trace.Severity}' of {tc.Id}", $"{def.File}:{def.Line}");
                ok = false;
            }
            if (def.StoryComment is null || !def.StoryComment.Contains(tc.UserStory, StringComparison.Ordinal))
            {
                findings.Add("O05", $"the comment above the definition must contain {tc.UserStory} (found '{def.StoryComment ?? "none"}')", $"{def.File}:{def.Line}");
                ok = false;
            }
            if (def.Method != PascalCase(key))
                findings.Add("O01", $"method name '{def.Method}' must be '{PascalCase(key)}'", $"{def.File}:{def.Line}");
            if (def.Template is null) { findings.Add("O04", $"definition of '{key}' has no literal Message template", $"{def.File}:{def.Line}"); ok = false; }

            RunbookEntry? entry = null;
            if (areaRunbooks.TryGetValue(tc.Area, out var doc))
            {
                entry = doc.Entries.FirstOrDefault(e => e.LogKey == key);
                if (entry is null) findings.Add("O06", $"{doc.Path} has no section for log key '{key}'", doc.Path);
                else if (entry.TestCaseId != tc.Id) findings.Add("O06", $"section '{entry.Anchor}' links test case '{entry.TestCaseId}', expected '{tc.Id}'", doc.Path);
            }
            if (!ok || entry is null) continue;

            signatures.Add(new Signature
            {
                Id = key,
                Kind = SignatureKinds.Keyed,
                Match = new SignatureMatch { LogKey = key },
                Area = tc.Area,
                UserStory = tc.UserStory,
                TestCase = tc.Id,
                Severity = trace.Severity,
                Message = trace.Message,
                CallSite = new CallSite { File = def.File, Line = def.Line, Method = def.Method, Template = def.Template!, Parameters = def.Parameters.ToList() },
                Runbook = new RunbookRef { Anchor = entry.Anchor, Path = doc!.Path, Text = entry.Text },
                Rate = new RateInfo { ExpectedPerHour = entry.ExpectedPerHour },
                FingerprintBy = entry.FingerprintBy,
                CauseCategory = entry.CauseCategory,
                Status = entry.Status,
                FixedIn = entry.FixedIn,
                Noise = NoiseOf(entry),
                RuleChecks = entry.RuleChecks,
                History = entry.History,
            });
            result.RunbookSections[entry.Anchor] = entry.Markdown;
        }

        // area runbook sections without a test case
        foreach (var (area, doc) in areaRunbooks)
            foreach (var e in doc.Entries)
                if (!traced.Any(t => t.Area == area && t.Trace!.LogKey == e.LogKey))
                    findings.Add("O06", $"section '{e.Anchor}' has no test case with log key '{e.LogKey}' in area '{area}'", $"{doc.Path}:{e.Line}");

        // 5. template runbooks
        var unkeyedPath = Path.Combine(repoRoot, "docs", "support", "unkeyed-runbooks.md");
        if (File.Exists(unkeyedPath))
        {
            var doc = RunbookParser.ParseUnkeyed(File.ReadAllText(unkeyedPath), UnkeyedRunbookPath, options, findings);
            foreach (var e in doc.Entries)
            {
                signatures.Add(new Signature
                {
                    Id = e.Anchor,
                    Kind = SignatureKinds.Template,
                    Match = new SignatureMatch
                    {
                        TemplateId = e.Anchor, EventName = e.EventName, Category = e.Category,
                        ExceptionType = e.ExceptionType, TopFrame = e.TopFrame, Template = e.Template,
                    },
                    Severity = e.Severity!,
                    Runbook = new RunbookRef { Anchor = e.Anchor, Path = UnkeyedRunbookPath, Text = e.Text },
                    Rate = new RateInfo { ExpectedPerHour = e.ExpectedPerHour },
                    FingerprintBy = e.FingerprintBy,
                    CauseCategory = e.CauseCategory,
                    Status = e.Status,
                    FixedIn = e.FixedIn,
                    Noise = NoiseOf(e),
                    RuleChecks = e.RuleChecks,
                    History = e.History,
                });
                result.RunbookSections[e.Anchor] = e.Markdown;
            }
        }
        else findings.Add("O07", "docs/support/unkeyed-runbooks.md not found", UnkeyedRunbookPath);

        // 6. order and validate
        result.Table.Signatures.AddRange(signatures.OrderBy(s => s.Id, StringComparer.Ordinal));
        for (var i = 1; i < result.Table.Signatures.Count; i++)
            if (result.Table.Signatures[i].Id == result.Table.Signatures[i - 1].Id)
                findings.Add("O06", $"signature id '{result.Table.Signatures[i].Id}' is not unique");

        if (signatureSchema is not null)
        {
            var validator = new SchemaSubsetValidator(signatureSchema);
            foreach (var s in result.Table.Signatures)
                foreach (var error in validator.Validate(MonitorJson.ToNode(s)))
                    findings.Add("O08", $"signature '{s.Id}' violates signature.schema.json: {error}", "signatures.json");
        }
        return result;
    }

    private static IEnumerable<string> AreasWithRunbook(string repoRoot)
    {
        var dir = Path.Combine(repoRoot, "docs", "areas");
        if (!Directory.Exists(dir)) yield break;
        foreach (var d in Directory.GetDirectories(dir))
            if (File.Exists(Path.Combine(d, "operations.md"))) yield return Path.GetFileName(d);
    }

    private static NoiseInfo? NoiseOf(RunbookEntry e) =>
        e.NoiseRationale is null || e.NoiseExpires is null ? null : new NoiseInfo { Rationale = e.NoiseRationale, Expires = e.NoiseExpires };

    public static string PascalCase(string logKey) =>
        string.Concat(logKey.Split('.', '_').Where(p => p.Length > 0).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    private static void CheckTraces(List<TestCase> traced, FindingList findings)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tc in traced)
        {
            var trace = tc.Trace!;
            var parts = tc.Id.Split('-');
            var prefix = $"{parts[0].ToLowerInvariant()}.{parts[1].ToLowerInvariant()}.";
            if (!trace.LogKey.StartsWith(prefix, StringComparison.Ordinal))
                findings.Add("R13", $"log key '{trace.LogKey}' must start with '{prefix}' (area code and action of {tc.Id})", tc.Path);
            var anchor = TestCaseParser.AnchorOf(trace.LogKey);
            if (trace.RunbookLinkText != anchor || trace.RunbookLinkTarget != $"../operations.md#{anchor}")
                findings.Add("R13", $"Runbook link must be [{anchor}](../operations.md#{anchor})", tc.Path);
            if (seen.TryGetValue(trace.LogKey, out var other))
                findings.Add("R13", $"log key '{trace.LogKey}' is also used by {other}", tc.Path);
            else seen[trace.LogKey] = tc.Id;
            if (trace.Message is { Length: > 300 }) findings.Add("R13", "Message is longer than 300 characters", tc.Path);
        }
    }
}
