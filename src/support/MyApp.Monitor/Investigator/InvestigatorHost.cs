using System.Threading.Channels;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Store;
using MyApp.Monitor.Tools;
using static MyApp.Monitor.Store.SqliteDatabase;

namespace MyApp.Monitor.Investigator;

/// <summary>
/// The investigator host of operations.md section 11: residual incidents are queued after they were written with their rule data, investigated one at a
/// time (<c>investigator.concurrency</c>), oldest first, in the background, so that a slow CPU tier never holds up the analyser. The model's verdict is stored next
/// to the rule verdict, never in place of it. A failure, a timeout or an invalid report leaves the incident with its rule verdict and records why; three
/// failures in a row switch the host to rules only until restart. When no host answers the monitor runs on rules only and says so (<see cref="Status"/>).
/// </summary>
public sealed class InvestigatorHost : BackgroundService, IInvestigator
{
    public const string Caller = "investigator";
    public static readonly EventId UnavailableEvent = new(2, "monitor.investigator.unavailable");
    public const int FailuresBeforeDisabled = 3;

    private readonly IRunContextProvider _contexts;
    private readonly IIncidentStore _store;
    private readonly MonitorOptions _options;
    private readonly InvestigatorSelector _selector;
    private readonly Protocol _protocol;
    private readonly TimeProvider _time;
    private readonly ILogger<InvestigatorHost> _logger;
    private readonly Channel<InvestigationRequest> _queue = Channel.CreateUnbounded<InvestigationRequest>(new UnboundedChannelOptions { SingleReader = false });
    private readonly SemaphoreSlim _selecting = new(1, 1);
    private volatile InvestigatorSelection _selection = new(new InvestigatorStatus(InvestigatorStates.RulesOnly, null, null, null, "the host has not been checked yet"), null, null);
    private int _consecutiveFailures;
    private int _completed;
    private int _failed;
    private int _disabled;
    private string? _lastLoggedReason;

    public InvestigatorHost(
        IRunContextProvider contexts, IIncidentStore store, MonitorOptions options, IInferenceBackendFactory factory, Protocol protocol, TimeProvider time,
        ILogger<InvestigatorHost> logger, Func<long>? totalMemoryMb = null)
    {
        _contexts = contexts;
        _store = store;
        _options = options;
        _protocol = protocol;
        _time = time;
        _logger = logger;
        _selector = new InvestigatorSelector(factory, time, totalMemoryMb ?? InvestigatorSelector.DefaultTotalMemoryMb);
    }

    public bool IsAvailable => _disabled == 0 && _selection.IsReady;

    public InvestigatorStatus Status => _selection.Status with
    {
        State = _disabled != 0 ? InvestigatorStates.Disabled : _selection.Status.State,
        Reason = _disabled != 0 ? $"{FailuresBeforeDisabled} investigations failed in a row: rules only until the monitor restarts" : _selection.Status.Reason,
        ConsecutiveFailures = _consecutiveFailures, Completed = _completed, Failed = _failed,
    };

    public async Task QueueAsync(InvestigationRequest request, CancellationToken ct) => await _queue.Writer.WriteAsync(request, ct).ConfigureAwait(false);

    // ---- the background host ----

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await RefreshAsync(stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { _logger.LogWarning(ex, "The investigator host could not be checked"); }

        var workers = Math.Max(1, _contexts.Current.Profile.Investigator.Concurrency);
        var tasks = Enumerable.Range(0, workers).Select(_ => Task.Run(() => WorkAsync(stoppingToken), CancellationToken.None)).ToList();
        tasks.Add(Task.Run(() => ProbeAsync(stoppingToken), CancellationToken.None));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task ProbeAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.Investigator.ProbeSeconds)));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try { await RefreshAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { _logger.LogWarning(ex, "Checking the investigator host failed"); }
            }
        }
        catch (OperationCanceledException) { /* stopping */ }
    }

    private async Task WorkAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var request in _queue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                try { await ProcessAsync(request, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { _logger.LogError(ex, "Investigating {Incident} failed unexpectedly", request.IncidentId); }
            }
        }
        catch (OperationCanceledException) { /* stopping */ }
    }

    /// <summary>The selection of 11.2: probes the hosts and records the result. Called at start, every probe interval, and when a request finds no host.</summary>
    public async Task RefreshAsync(CancellationToken ct)
    {
        await _selecting.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var ctx = _contexts.Current;
            var settings = ctx.Profile.Investigator;
            var device = _options.Investigator.Device is { Length: > 0 } d ? d : settings.Device;
            var selection = await _selector.SelectAsync(
                new SelectionInput(settings.Tier, device, ResolveLock(), ProtocolBlock.Sha256(ctx.Pack.ProtocolText), _options.Investigator), ct).ConfigureAwait(false);
            _selection = selection;
            if (selection.Status.State == InvestigatorStates.RulesOnly && selection.Status.Reason != _lastLoggedReason)
                _logger.Log(LogLevel.Warning, UnavailableEvent, "The investigator is unavailable and the monitor runs on rules only: {Reason}", selection.Status.Reason);
            else if (selection.IsReady && _lastLoggedReason is not null)
                _logger.LogInformation("The investigator host is available: tier {Tier} on the {Device}, model version {Version}", selection.Status.Tier, selection.Status.Device, selection.Status.ModelVersion);
            _lastLoggedReason = selection.IsReady ? null : selection.Status.Reason;
        }
        finally { _selecting.Release(); }
    }

    private string? ResolveLock()
    {
        if (!string.IsNullOrWhiteSpace(_options.Investigator.ModelLock)) return Path.GetFullPath(_options.Investigator.ModelLock, _options.ResolveContentRoot());
        for (var dir = new DirectoryInfo(_options.ResolveContentRoot()); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "models", "model.lock");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    // ---- one investigation ----

    /// <summary>Investigates one incident and writes the outcome. Public so that tests and the dashboard's "Investigate again" do not depend on timing.</summary>
    public async Task ProcessAsync(InvestigationRequest request, CancellationToken ct)
    {
        var row = await _store.GetAsync(request.IncidentId, ct).ConfigureAwait(false);
        if (row is null) return;
        var ctx = _contexts.Current;

        var selection = _selection;
        if (_disabled == 0 && !selection.IsReady)
        {
            await RefreshAsync(ct).ConfigureAwait(false);
            selection = _selection;
        }
        if (_disabled != 0 || !selection.IsReady)
        {
            var why = Status.Reason;
            await _store.SetInvestigationAsync(row.Id, "skipped", _time.GetUtcNow(), why, ct).ConfigureAwait(false);
            await _store.AddEventAsync(row.Id, _time.GetUtcNow(), "investigation", new JsonObject { ["status"] = "skipped", ["reason"] = why }, ct).ConfigureAwait(false);
            return;
        }

        await _store.SetInvestigationAsync(row.Id, "running", _time.GetUtcNow(), null, ct).ConfigureAwait(false);
        var report = row.Report;
        var signals = (await _store.EventsAsync(row.Id, ct).ConfigureAwait(false))
            .Where(e => e.Kind == IncidentPrompt.SignalEvent).OrderBy(e => e.Id)
            .Select(e => MonitorJson.Parse(e.DataJson) as JsonObject).OfType<JsonObject>().ToList();
        var parts = IncidentPrompt.Build(report, signals, ctx.Pack);
        var executor = ctx.Runtime.Executor;
        var settings = ctx.Profile.Investigator;
        var tier = selection.Status.Tier!;

        var cap = Math.Min(_protocol.BudgetCap, ctx.Pack.Constraints.MaxToolCalls ?? _protocol.BudgetCap);
        var budget = Math.Clamp(settings.MaxToolCalls ?? _protocol.DefaultBudget(tier), 1, cap);
        var input = new InvestigationInput
        {
            Protocol = _protocol,
            System = ProtocolBlock.SystemText(ctx.Pack.ProtocolText),
            Incident = parts.Incident,
            Pack = parts.Pack,
            Tools = executor.OfferedTools.Select(t => (t.Name, t.Description, (JsonNode)t.Parameters)).ToList(),
            Entities = ctx.Pack.Entities.Entities.Select(e => (e.Name, e.Description)).ToList(),
            Constraints = ctx.Pack.Constraints.Constraints.Select(c => (c.Id, c.Text)).ToList(),
            InvestigationSchema = _protocol.InvestigationSchema(ctx.Pack.IncidentReportSchema),
            Budget = budget,
            Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds)),
            ContextTokens = selection.Export?.ContextLength,
            ToolRole = selection.Export?.ToolRole ?? Protocol.ProtocolToolRole,
            Stories = parts.Stories,
            Caller = Caller,
        };
        var session = new IncidentToolSession(row.Id, LastEvidenceNumber(report));

        InvestigationResult result;
        try { result = await new InvestigationLoop(selection.Backend!, executor, _time).RunAsync(input, session, ct).ConfigureAwait(false); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await FinishFailedAsync(row.Id, "the monitor stopped during the investigation", [], ct: CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The investigation of {Incident} failed", row.Id);
            await FinishFailedAsync(row.Id, "the investigation failed: " + ex.GetType().Name, [], ct: CancellationToken.None).ConfigureAwait(false);
            return;
        }

        if (result.Outcome == InvestigationOutcome.Failed)
        {
            await FinishFailedAsync(row.Id, result.Error ?? "failed", result.Calls, result.FailureKind, ct: CancellationToken.None).ConfigureAwait(false);
            return;
        }

        var written = await WriteReportAsync(ctx, row.Id, result, selection, tier, ct).ConfigureAwait(false);
        if (!written.Ok)
        {
            await FinishFailedAsync(row.Id, written.Problem!, result.Calls, "invalid_report", ct: CancellationToken.None).ConfigureAwait(false);
            return;
        }
        Interlocked.Exchange(ref _consecutiveFailures, 0);
        Interlocked.Increment(ref _completed);
    }

    private static int LastEvidenceNumber(JsonObject report) =>
        ((report["evidence"] as JsonArray) ?? []).Select(e => int.TryParse(MonitorJson.ScalarText(e?["id"])?[1..], out var n) ? n : 1).DefaultIfEmpty(1).Max();

    private sealed record Written(bool Ok, string? Problem);

    private async Task<Written> WriteReportAsync(RunContext ctx, string id, InvestigationResult result, InvestigatorSelection selection, string tier, CancellationToken ct)
    {
        var masker = ctx.Runtime.Masker;
        var investigation = result.Report!;
        MaskTexts(investigation, masker);
        var abstained = MonitorJson.ScalarText(investigation["verdict"]) == "abstain";
        var now = _time.GetUtcNow();
        var tools = result.Calls.Select(c => c.Tool).ToList();
        var model = new JsonObject
        {
            ["model_id"] = InvestigatorSelector.ModelId, ["version"] = selection.Status.ModelVersion, ["tier"] = tier, ["device"] = selection.Status.Device,
            ["tool_calls"] = result.Calls.Count, ["abstained"] = abstained,
        };

        IncidentChange Apply(JsonObject report)
        {
            MergeEvidence(report, result.Calls, masker, now);
            report["investigation"] = investigation.DeepClone();
            report["model_verdict"] = abstained ? null : new JsonObject
            {
                ["cause_category"] = investigation["cause_category"]!.DeepClone(),
                ["cause"] = investigation["cause"]!.DeepClone(),
                ["confidence"] = investigation["confidence"]!.DeepClone(),
                ["evidence_refs"] = investigation["evidence_refs"]!.DeepClone(),
            };
            if (report["reproduction"] is null && investigation["reproduction"] is JsonObject repro) report["reproduction"] = repro.DeepClone();
            report["model"] = model.DeepClone();
            IncidentReports.AddAction(report, now, abstained ? ReportActions.InvestigationAbstained : ReportActions.InvestigationDone,
                $"tier {tier}, {result.Calls.Count} tool call(s){(tools.Count > 0 ? ": " + string.Join(", ", tools) : "")}");
            return IncidentChange.Of("investigation", new JsonObject
            {
                ["status"] = abstained ? "abstained" : "done", ["tier"] = tier, ["device"] = selection.Status.Device, ["model_version"] = selection.Status.ModelVersion,
                ["tool_calls"] = result.Calls.Count, ["tools"] = new JsonArray(tools.Select(t => (JsonNode?)t).ToArray()),
                ["verdict"] = investigation["verdict"]!.DeepClone(), ["abstain_reason"] = investigation["abstain_reason"]?.DeepClone(),
                ["repaired"] = result.Repaired, ["seconds"] = Math.Round(result.Duration.TotalSeconds, 1),
            });
        }

        // the stored report must still validate against the schema: a model that breaks it never changes the incident
        var trial = (await _store.GetAsync(id, ct).ConfigureAwait(false))?.Report;
        if (trial is null) return new Written(false, "the incident no longer exists");
        Apply(trial);
        if (!ctx.ReportValidator.IsValid(trial, out var errors))
            return new Written(false, "the report with the model's verdict does not validate: " + string.Join("; ", errors.Take(3)));

        var row = await _store.UpdateAsync(id, now, Apply, ct).ConfigureAwait(false);
        if (row is null) return new Written(false, "the incident no longer exists");
        await _store.SetInvestigationAsync(id, abstained ? "abstained" : "done", now, null, ct).ConfigureAwait(false);
        return new Written(true, null);
    }

    private async Task FinishFailedAsync(string id, string reason, IReadOnlyList<InvestigationCall> calls, string? kind = null, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow();
        Interlocked.Increment(ref _failed);
        var streak = Interlocked.Increment(ref _consecutiveFailures);
        if (streak >= FailuresBeforeDisabled && Interlocked.Exchange(ref _disabled, 1) == 0)
            _logger.Log(LogLevel.Warning, UnavailableEvent, "{Count} investigations failed in a row: the monitor runs on rules only until it restarts", streak);
        var masked = _contexts.Current.Runtime.Masker.MaskFreeText(reason, 300);
        await _store.SetInvestigationAsync(id, "failed", now, masked, ct).ConfigureAwait(false);
        await _store.UpdateAsync(id, now, report =>
        {
            IncidentReports.AddAction(report, now, ReportActions.InvestigationFailed, masked);
            return IncidentChange.Of("investigation", new JsonObject
            {
                ["status"] = "failed", ["reason"] = masked, ["kind"] = kind, ["tool_calls"] = calls.Count,
            });
        }, ct).ConfigureAwait(false);
    }

    private static void MaskTexts(JsonObject investigation, Masking.Masker masker)
    {
        if (investigation["cause"] is JsonValue c && c.TryGetValue<string>(out var cause)) investigation["cause"] = masker.MaskReportText(cause);
        if (investigation["next_checks"] is JsonArray checks)
            for (var i = 0; i < checks.Count; i++) checks[i] = masker.MaskReportText(MonitorJson.ScalarText(checks[i]) ?? "");
        if (investigation["reproduction"] is JsonObject repro)
        {
            foreach (var key in new[] { "expected", "observed" }) if (repro[key] is JsonValue v && v.TryGetValue<string>(out var s)) repro[key] = masker.MaskReportText(s);
            foreach (var key in new[] { "preconditions", "steps" })
                if (repro[key] is JsonArray list) for (var i = 0; i < list.Count; i++) list[i] = masker.MaskReportText(MonitorJson.ScalarText(list[i]) ?? "");
        }
    }

    /// <summary>
    /// Adds the evidence the investigator received to the report (no cause without an evidence reference a person can open). A signal item appended meanwhile
    /// that has the same number is renumbered: it was never cited.
    /// </summary>
    private static void MergeEvidence(JsonObject report, IReadOnlyList<InvestigationCall> calls, Masking.Masker masker, DateTimeOffset now)
    {
        var evidence = (JsonArray)report["evidence"]!;
        int Max() => evidence.Select(e => int.Parse(MonitorJson.ScalarText(e?["id"])![1..], System.Globalization.CultureInfo.InvariantCulture)).DefaultIfEmpty(1).Max();
        foreach (var call in calls)
        {
            var env = call.Envelope;
            if (!env.IsSuccess || env.EvidenceId is null) continue;
            var existing = evidence.FirstOrDefault(e => MonitorJson.ScalarText(e?["id"]) == env.EvidenceId);
            if (existing is not null)
            {
                if (MonitorJson.ScalarText(existing["source"]) != "signal" || env.EvidenceId == "E1") continue;
                existing["id"] = "E" + (Max() + 1);
            }
            while (evidence.Count >= IncidentReports.MaxEvidence)
            {
                var drop = evidence.LastOrDefault(e => MonitorJson.ScalarText(e?["source"]) == "signal" && MonitorJson.ScalarText(e?["id"]) != "E1");
                if (drop is null) return;
                evidence.Remove(drop);
            }
            var args = string.Join(' ', call.Arguments.Select(kv => $"{kv.Key}={MonitorJson.ScalarText(kv.Value)}"));
            evidence.Add(new JsonObject
            {
                ["id"] = env.EvidenceId,
                ["source"] = IncidentReports.Cut(call.Tool, 64),
                ["reference"] = masker.MaskFreeText($"{call.Tool} {args}".Trim(), 1000),
                ["observed_at"] = Iso(now),
                ["excerpt"] = IncidentReports.Cut(env.Data is null ? $"{env.Status}: {env.Error}" : MonitorJson.ToCompact(env.Data), 1000),
            });
        }
    }
}
