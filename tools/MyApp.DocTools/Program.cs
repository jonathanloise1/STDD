using MyApp.DocTools.Model;
using MyApp.DocTools.Rules;

namespace MyApp.DocTools;

public static class Program
{
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter output, TextWriter error, IGitReader? git = null)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            output.WriteLine("Usage: MyApp.DocTools <check|sync-automated-by|write-baseline> [repository root] [--baseline <file>] [--target-ref <git ref>]");
            return args.Length == 0 ? 2 : 0;
        }
        string? root = null, baselinePath = null, targetRef = null;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--baseline" && i + 1 < args.Length) baselinePath = args[++i];
            else if (args[i] == "--target-ref" && i + 1 < args.Length) targetRef = args[++i];
            else root = args[i];
        }
        root = root is null ? Repo.FindRoot(Directory.GetCurrentDirectory()) : Path.GetFullPath(root);
        if (root is null || !File.Exists(Path.Combine(root, "docs", "contracts.json")))
        {
            error.WriteLine("Repository root not found: no folder containing docs/contracts.json. Pass the root as an argument.");
            return 2;
        }
        var repo = new Repo(root);
        baselinePath ??= repo.Abs(Repo.BaselinePath);
        switch (args[0])
        {
            case "check": return Check(repo, baselinePath, targetRef, git ?? new GitCli(repo.Root), output, error);
            case "sync-automated-by":
                var changed = AutomatedBy.Sync(repo);
                foreach (var c in changed) output.WriteLine($"updated {c}");
                output.WriteLine($"{changed.Count} test case file(s) updated.");
                return 0;
            case "write-baseline":
                output.WriteLine("WARNING: write-baseline records every current finding as accepted. Use it for first adoption only; the baseline may only shrink afterwards and entries are never added to make a change pass.");
                var findings = RuleSet.Run(repo);
                Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
                File.WriteAllText(baselinePath, Baseline.Serialize(findings), new System.Text.UTF8Encoding(false));
                output.WriteLine($"Wrote {findings.Select(f => (f.Rule, f.Subject)).Distinct().Count()} entries to {baselinePath}");
                return 0;
            default:
                error.WriteLine($"Unknown command \"{args[0]}\".");
                return 2;
        }
    }

    private static int Check(Repo repo, string baselinePath, string? targetRef, IGitReader git, TextWriter output, TextWriter error)
    {
        Baseline baseline;
        try { baseline = Baseline.Load(baselinePath); }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or KeyNotFoundException)
        { error.WriteLine($"Cannot read the baseline: {e.Message}"); return 2; }

        var added = new List<BaselineEntry>();
        if (targetRef is not null)
        {
            if (!git.RefExists(targetRef)) { error.WriteLine($"The git ref \"{targetRef}\" cannot be read."); return 2; }
            var targetBaseline = git.ReadFile(targetRef, Repo.BaselinePath);
            if (targetBaseline is null) output.WriteLine($"TARGET {targetRef} has no baseline file: first adoption, nothing to compare.");
            else
            {
                try { added = Baseline.AddedSince(baseline, Baseline.Parse(targetBaseline), Repo.RuleIdsIn(git.ReadFile(targetRef, Repo.DevelopmentPath) ?? "")); }
                catch (Exception e) when (e is System.Text.Json.JsonException or KeyNotFoundException)
                { error.WriteLine($"Cannot read the baseline of {targetRef}: {e.Message}"); return 2; }
            }
        }

        var findings = RuleSet.Run(repo);
        var result = Baseline.Compare(findings, baseline);
        var newSet = result.New.ToHashSet();
        foreach (var rule in RuleSet.All)
        {
            var list = findings.Where(f => f.Rule == rule.Id).ToList();
            if (list.Count == 0) continue;
            output.WriteLine($"{rule.Id} {rule.Name}: {list.Count} finding(s), {list.Count(newSet.Contains)} new");
            foreach (var f in list)
                output.WriteLine($"  {f.Subject}{(newSet.Contains(f) ? "" : "  [baselined]")}\n      {f.Message}");
        }
        foreach (var s in result.Stale) output.WriteLine($"STALE {s.Rule} {s.Subject}: no finding matches this baseline entry; remove it.");
        foreach (var a in added) output.WriteLine($"ADDED {a.Rule} {a.Subject}: this entry is not in the baseline of {targetRef}. The baseline may only shrink: fix the finding and remove the entry.");
        var problems = baseline.Problems();
        foreach (var p in problems) output.WriteLine($"BASELINE {p}");
        output.WriteLine($"Summary: {findings.Count} finding(s), {result.New.Count} new, {result.Known.Count} baselined, {result.Stale.Count} stale baseline entr{(result.Stale.Count == 1 ? "y" : "ies")}{(targetRef is null ? "" : $", {added.Count} added since {targetRef}")}.");
        return result.New.Count > 0 || result.Stale.Count > 0 || problems.Count > 0 || added.Count > 0 ? 1 : 0;
    }
}
