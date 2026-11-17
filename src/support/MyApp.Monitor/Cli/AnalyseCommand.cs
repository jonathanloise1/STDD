using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Store;

namespace MyApp.Monitor.Cli;

/// <summary>
/// <c>analyse --input {file.jsonl} [--explain] [--dry-run]</c>: runs the analyser on a log file once and prints matches,
/// template ids and escalations. Without <c>--dry-run</c> the counters and the unknown templates are written to the profile's store (the
/// analyser's own writes); with it nothing is written. Escalations are only printed: opening an incident, and notifying, is the job of <c>run</c>.
/// </summary>
public sealed class AnalyseCommand : ICommandHandler
{
    public string Name => "analyse";
    public string Usage => "analyse --input {file.jsonl} [--explain] [--dry-run] [--pack {path}] [--profile {name}]";
    public IReadOnlyDictionary<string, bool> Options { get; } = new Dictionary<string, bool>
    {
        ["input"] = true, ["explain"] = false, ["dry-run"] = false, ["pack"] = true, ["profile"] = true, ["repo"] = true,
    };

    public async Task<int> RunAsync(CommandArgs args, CommandContext context, CancellationToken ct)
    {
        var input = args.Get("input") ?? throw new UsageException("--input is required");
        if (!File.Exists(input)) throw new UsageException($"--input: file not found: {input}");

        var pack = PackSelection.Resolve(args, context);
        var factory = context.Services.GetRequiredService<MonitorRuntimeFactory>();
        var runtime = factory.Create(pack);
        var store = args.Has("dry-run")
            ? null
            : new SqliteStore(SqliteDatabase.Open(Path.GetFullPath(runtime.Profile.Store.Path, context.Options.ResolveContentRoot())));
        try
        {
            return await AnalyseAsync(input, args.Has("explain"), runtime, factory, store, context, ct).ConfigureAwait(false);
        }
        finally { store?.Database.Close(); }
    }

    private static async Task<int> AnalyseAsync(
        string input, bool explain, MonitorRuntime runtime, MonitorRuntimeFactory factory, SqliteStore? store, CommandContext context, CancellationToken ct)
    {
        var analyser = store is null ? factory.CreateAnalyser(runtime) : factory.CreateAnalyser(runtime, store, store, store);
        var pipeline = new AnalysisPipeline(runtime.Masker, analyser, new InMemorySourceStateStore(), context.Time);

        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var escalations = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var total = 0;
        var skipped = 0;

        foreach (var line in File.ReadLines(input))
        {
            ct.ThrowIfCancellationRequested();
            if (line.Length == 0) continue;
            var record = JsonlRecordParser.Parse(line, "input", runtime.Environment);
            if (record is null) { skipped++; continue; }
            var results = await pipeline.ProcessAsync(new SignalBatch("input", [record], null), ct).ConfigureAwait(false);
            if (results.Count == 0) continue;   // a duplicate record
            var r = results[0];
            total++;
            context.Out.WriteLine(Describe(r, explain));
            counts[r.Match.Kind.ToString()] = counts.GetValueOrDefault(r.Match.Kind.ToString()) + 1;
            if (r.Escalation is { } e) escalations[e.Trigger] = escalations.GetValueOrDefault(e.Trigger) + 1;
        }

        context.Out.WriteLine($"-- {total} records, {skipped} lines skipped; " +
            string.Join(", ", counts.Select(kv => $"{kv.Key} {kv.Value}")) +
            (escalations.Count == 0 ? "; no escalations" : "; escalations: " + string.Join(", ", escalations.Select(kv => $"{kv.Key} {kv.Value}"))));
        if (store is not null) context.Out.WriteLine($"-- counters and unknown templates written to {store.Database.Path}");
        return ExitCodes.Ok;
    }

    public static string Describe(AnalysisResult r, bool explain)
    {
        var sb = new StringBuilder();
        var rec = r.Record;
        sb.Append(LogRecord.FormatTimestamp(rec.Timestamp)).Append(' ').Append(rec.Level).Append(' ');
        sb.Append(rec.LogKey ?? rec.EventName ?? "(unkeyed)");
        switch (r.Match.Kind)
        {
            case MatchKind.Matched:
                var s = r.Match.Signature!;
                sb.Append($" matched {s.Kind} {s.Id} runbook={s.Runbook.Anchor} status={s.Status} severity={s.Severity}");
                break;
            case MatchKind.UnknownKey:
                sb.Append($" unknown_key {r.Match.Id}");
                break;
            default:
                sb.Append($" unknown_template {r.Match.Id}");
                break;
        }
        sb.Append($" rate={RateFormat.Format(r.SignatureRatePerHour)}/h count={r.SignatureWindowCount} fingerprint={r.Fingerprint}");
        if (r.Escalation is { } e)
            sb.Append($"\n  ESCALATE trigger={e.Trigger} severity={e.Severity} verdict={(e.Residual ? "residual" : "rule")} action={e.Action}");
        else if (r.CountedOnlyReason is not null)
            sb.Append($"\n  counted only: {r.CountedOnlyReason}");
        if (explain)
        {
            var t = r.Template ?? TemplateExtractor.Extract(rec);
            foreach (var (name, value) in t.Parts) sb.Append($"\n  {name}: {value}");
            sb.Append($"\n  template_id: {t.Id}");
        }
        return sb.ToString();
    }
}
