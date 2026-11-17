using System.Text;
using System.Text.Json.Nodes;
using MyApp.Monitor.Pack;
using MyApp.Monitor.Return;
using MyApp.Monitor.Store;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Cli;

/// <summary>
/// <c>return {--report file.json | --incident id --store file.db} [--out dir] [--release version] [--repo dir]</c> (operations.md 15): the content of the pull request
/// for a closed incident, as files in <c>--out</c> (default <c>artifacts/return/{incident id}</c>): <c>branch.txt</c>, <c>pull-request.md</c>, <c>runbook.md</c> (the
/// section as it must read), <c>signature-change.json</c> and <c>notes.txt</c>. It only reads the repository and never runs git or calls GitHub: creating the branch,
/// committing, pushing and opening the pull request need the owner's approval.
/// </summary>
public sealed class ReturnCommand : ICommandHandler
{
    public string Name => "return";
    public string Usage => "return {--report {file.json} | --incident {id} --store {monitor.db}} [--out {dir}] [--release {version}] [--repo {dir}]";
    public IReadOnlyDictionary<string, bool> Options { get; } = new Dictionary<string, bool>
    {
        ["report"] = true, ["incident"] = true, ["store"] = true, ["out"] = true, ["release"] = true, ["repo"] = true,
    };

    public async Task<int> RunAsync(CommandArgs args, CommandContext context, CancellationToken ct)
    {
        var repo = RepositoryLocator.Find(args.Get("repo") ?? context.Options.RepositoryRoot);
        JsonObject report;
        if (args.Get("report") is { } file)
        {
            if (!File.Exists(file)) throw new UsageException($"--report: file not found: {file}");
            try { report = MonitorJson.Parse(File.ReadAllText(file)) as JsonObject ?? throw new UsageException("--report must hold one JSON object"); }
            catch (System.Text.Json.JsonException ex) { throw new UsageException($"--report is not valid JSON: {ex.Message}"); }
        }
        else if (args.Get("incident") is { } id && args.Get("store") is { } store)
        {
            if (!File.Exists(store)) throw new UsageException($"--store: file not found: {store}");
            var database = SqliteDatabase.Open(store);
            try { report = (await new SqliteStore(database).GetAsync(id, ct).ConfigureAwait(false))?.Report ?? throw new MonitorException($"no incident {id} in {store}"); }
            finally { database.Close(); }
        }
        else throw new UsageException("give --report {file}, or --incident {id} with --store {file}");

        var schemaFile = Path.Combine(repo, "docs", "support", "incident-report.schema.json");
        var schema = new SchemaSubsetValidator(MonitorJson.Parse(File.ReadAllText(schemaFile))!);
        var plan = ReturnPlanner.Plan(report, schema, new ReturnPlanner.Options(repo, args.Get("release"), DateOnly.FromDateTime(context.Time.GetUtcNow().UtcDateTime)));

        foreach (var note in plan.Notes) context.Error.WriteLine("note: " + note);
        if (!plan.NeedsChange)
        {
            context.Out.WriteLine($"{plan.IncidentId}: outcome {plan.Outcome}, no repository change.");
            return ExitCodes.Ok;
        }

        var outDir = Path.GetFullPath(args.Get("out") ?? Path.Combine(repo, "artifacts", "return", plan.IncidentId));
        Directory.CreateDirectory(outDir);
        var utf8 = new UTF8Encoding(false);
        File.WriteAllText(Path.Combine(outDir, "branch.txt"), plan.BranchName + "\n", utf8);
        File.WriteAllText(Path.Combine(outDir, "pull-request.md"), plan.PullRequestBody, utf8);
        if (plan.RunbookText is not null) File.WriteAllText(Path.Combine(outDir, "runbook.md"), $"<!-- replaces the section in {plan.RunbookPath} -->\n\n" + plan.RunbookText, utf8);
        if (plan.SignatureChange is not null) File.WriteAllText(Path.Combine(outDir, "signature-change.json"), plan.SignatureChange.ToJsonString(MonitorJson.Snake) + "\n", utf8);
        if (plan.Notes.Count > 0) File.WriteAllText(Path.Combine(outDir, "notes.txt"), string.Join('\n', plan.Notes) + "\n", utf8);
        context.Out.WriteLine($"{plan.IncidentId}: outcome {plan.Outcome}. Branch {plan.BranchName}. Files written to {outDir}. Nothing was committed, pushed or opened.");
        return ExitCodes.Ok;
    }
}
