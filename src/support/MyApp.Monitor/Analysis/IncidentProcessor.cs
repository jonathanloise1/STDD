using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MyApp.Monitor.Investigator;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Rules;
using MyApp.Monitor.Sources;
using MyApp.Monitor.Store;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Analysis;

public sealed record ManualIncidentRequest(string User, string? TraceId, string? Entity, string? EntityId, string? Note);

/// <summary>
/// Step 6 of the incident loop (operations.md 2): turns an escalation into one write to the incident store, then notifies. Suppression
/// windows apply first (counters only), a fingerprint that is already open folds into its incident, and a new one runs the rule engine
/// and writes a report that validates against the schema. A residual case is written with its rule verdict and <c>model</c> empty; the
/// investigator seam is the only hook.
/// </summary>
public sealed class IncidentProcessor(
    IIncidentStore store, SuppressionService suppression, NotificationDispatcher notifier, IInvestigator investigator, TimeProvider time,
    ILogger<IncidentProcessor>? logger = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task ProcessAsync(RunContext ctx, AnalysisResult result, CancellationToken ct = default)
    {
        if (result.Escalation is not { } e) return;   // counters only: the analyser already counted it
        var now = time.GetUtcNow();
        if (await suppression.CheckAsync(e.SignatureOrTemplateId, now, ct).ConfigureAwait(false) is { } window)
        {
            logger?.LogDebug("Escalation of {Id} suppressed by window {Window}", e.SignatureOrTemplateId, window);
            return;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // The analyser decided before earlier records of the same batch were written: the store is the authority on what is open.
            var open = ((IIncidentIndex)store).FindOpen(e.Fingerprint);
            if (open is not null && await AppendAsync(ctx, open.Id, e, now, ct).ConfigureAwait(false)) return;
            await CreateAsync(ctx, e, now, ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Folds a record into the open incident. False when that incident was closed meanwhile.</summary>
    private async Task<bool> AppendAsync(RunContext ctx, string incidentId, Escalation e, DateTimeOffset now, CancellationToken ct)
    {
        var closedMeanwhile = false;
        var raised = false;
        var row = await store.UpdateAsync(incidentId, now, report =>
        {
            if (MonitorJson.ScalarText(report["status"]) == "closed") { closedMeanwhile = true; return IncidentChange.None; }
            var change = IncidentReports.Append(report, e, now, ctx.Runtime.Masker);
            raised = change.Events.Any(x => x.Kind == "severity_raised");
            return change;
        }, ct).ConfigureAwait(false);
        if (row is null || closedMeanwhile) return false;
        if (raised) await notifier.NotifyAsync(ctx.Notifications, incidentId, NotificationEvents.SeverityRaised, ct).ConfigureAwait(false);
        return true;
    }

    private async Task<IncidentRow> CreateAsync(RunContext ctx, Escalation e, DateTimeOffset now, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var id = await store.NextIncidentIdAsync(now, ct).ConfigureAwait(false);
            var rule = await ctx.Rules.RunAsync(e, new IncidentToolSession(id), ct).ConfigureAwait(false);
            var report = IncidentReports.Build(rule, id, now, ctx.Pack.Version, ctx.Runtime.Masker);
            if (!ctx.ReportValidator.IsValid(report, out var errors))
                logger?.LogError("The report of {Incident} does not validate against the incident report schema: {Errors}", id, string.Join("; ", errors.Take(5)));

            var events = new List<(string, JsonNode)>
            {
                ("created", new JsonObject { ["trigger"] = e.Trigger, ["severity"] = e.Severity, ["fingerprint"] = e.Fingerprint, ["residual"] = rule.Residual }),
            };
            events.Add((IncidentPrompt.SignalEvent, SignalSnapshot.ToJson(e.Record)));   // the masked triggering record: E1 of the investigator's prompt
            foreach (var c in rule.Checks)
                events.Add(("check_run", new JsonObject { ["tool"] = c.Tool, ["expect"] = c.Expect, ["passed"] = c.Passed, ["evidence_id"] = c.EvidenceId, ["error"] = c.Error }));

            var verdictSource = rule.Verdict is null ? "none" : "rule";
            if (!await store.CreateAsync(report, verdictSource, now, events, ct).ConfigureAwait(false))
            {
                if (attempt >= 2) throw new MonitorException($"could not allocate an incident id for fingerprint {e.Fingerprint}");
                continue;
            }

            if (rule.Residual) await QueueInvestigationAsync(id, e.Residual ? e.Trigger : "failed_rule_check", now, ct).ConfigureAwait(false);
            await notifier.NotifyAsync(ctx.Notifications, id, NotificationEvents.Created, ct).ConfigureAwait(false);
            return (await store.GetAsync(id, ct).ConfigureAwait(false))!;
        }
    }

    /// <summary>The seam of the third pass: queued when a host is available, otherwise recorded as skipped and the rule verdict stands.</summary>
    public async Task<bool> QueueInvestigationAsync(string incidentId, string reason, DateTimeOffset now, CancellationToken ct)
    {
        if (!investigator.IsAvailable)
        {
            await store.SetInvestigationAsync(incidentId, "skipped", now, "no investigator available: the incident keeps its rule verdict", ct).ConfigureAwait(false);
            await store.AddEventAsync(incidentId, now, "investigation", new JsonObject { ["status"] = "skipped", ["reason"] = reason }, ct).ConfigureAwait(false);
            return false;
        }
        await store.SetInvestigationAsync(incidentId, "queued", now, null, ct).ConfigureAwait(false);
        await store.UpdateAsync(incidentId, now, report =>
        {
            IncidentReports.AddAction(report, now, ReportActions.InvestigationQueued, reason);
            return IncidentChange.Of("investigation", new JsonObject { ["status"] = "queued", ["reason"] = reason });
        }, ct).ConfigureAwait(false);
        await investigator.QueueAsync(new InvestigationRequest(incidentId, reason), ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>A manual incident (trigger <c>manual</c>) from the dashboard search. One incident per trace or entity while it is open.</summary>
    public async Task<IncidentRow> OpenManualAsync(RunContext ctx, ManualIncidentRequest request, CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var masker = ctx.Runtime.Masker;
        var properties = new Dictionary<string, string>();
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : masker.MaskFreeText(request.Note.Trim(), 300);
        var record = LogRecord.Create(
            "dashboard", ctx.Runtime.Environment, now, RecordLevel.Warning, "MyApp.Monitor.Dashboard",
            "Incident opened manually from the dashboard" + (note is null ? "" : ": " + note),
            eventName: "monitor.incident.manual", messageTemplate: "Incident opened manually from the dashboard",
            traceId: request.TraceId, properties: properties);
        var template = TemplateExtractor.Extract(record);
        var subject = request.TraceId ?? $"{request.Entity}:{request.EntityId}";
        var fingerprint = Analyser.ComputeFingerprint(ctx.Runtime.Environment, template.Id, [subject]);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (((IIncidentIndex)store).FindOpen(fingerprint) is { } open) return (await store.GetAsync(open.Id, ct).ConfigureAwait(false))!;

            var match = new MatchResult(MatchKind.UnknownTemplate, null, template.Id, template);
            var escalation = new Escalation(record, match, Triggers.Manual, "Warning", false, fingerprint, template.Id, 1, 0, 1, 0, EscalationAction.OpenNew, null, null);
            var id = await store.NextIncidentIdAsync(now, ct).ConfigureAwait(false);
            var session = new IncidentToolSession(id);
            var rule = await ctx.Rules.RunAsync(escalation, session, ct).ConfigureAwait(false);
            if (request.Entity is not null && request.EntityId is not null)
                await ctx.Rules.AddEntityAsync(rule, session, request.Entity, request.EntityId, ct).ConfigureAwait(false);
            var report = IncidentReports.Build(rule, id, now, ctx.Pack.Version, masker);
            if (!ctx.ReportValidator.IsValid(report, out var errors))
                logger?.LogError("The report of {Incident} does not validate: {Errors}", id, string.Join("; ", errors.Take(5)));
            await store.CreateAsync(report, "none", now,
                [("created", new JsonObject { ["trigger"] = Triggers.Manual, ["opened_by"] = request.User }), (IncidentPrompt.SignalEvent, SignalSnapshot.ToJson(record))], ct).ConfigureAwait(false);
            await notifier.NotifyAsync(ctx.Notifications, id, NotificationEvents.Created, ct).ConfigureAwait(false);
            return (await store.GetAsync(id, ct).ConfigureAwait(false))!;
        }
        finally { _gate.Release(); }
    }
}
