using System.Text.Json.Nodes;
using MyApp.Monitor.Analysis;
using MyApp.Monitor.Notifications;
using MyApp.Monitor.Store;
using MyApp.Monitor.Tools;
using static MyApp.Monitor.Store.SqliteDatabase;

namespace MyApp.Monitor.Dashboard;

public sealed record ActionResult(bool Ok, string Message);

public sealed record CloseRequest(string Value, string? Ticket, string? PullRequest, string? Note, string? DuplicateOf);

public sealed record IngestResult(int Status, string Message, string? Id, IReadOnlyList<string> Errors);

/// <summary>The outcomes of operations.md section 15 and what each one asks of the repository.</summary>
public static class Outcomes
{
    public static readonly IReadOnlyList<string> All = ["fixed", "noise", "reclassified", "duplicate", "wontfix", "unknown_resolved"];

    /// <summary>Repository change per outcome (section 15). <c>duplicate</c> needs none.</summary>
    public static readonly IReadOnlyDictionary<string, string> RepositoryChange = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["fixed"] = "The test case that catches the failure (new or corrected) passes; runbook Status Fixed, Fixed in the release; History line.",
        ["noise"] = "Runbook Status Noise with rationale and expiry; for unkeyed, a runbook in unkeyed-runbooks.md; reviewer other than the author.",
        ["reclassified"] = "Test case Severity and the definition's level changed together; runbook text updated; History line.",
        ["unknown_resolved"] = "A new Error handling or Edge case test case with its Observable trace, a log definition at the point of detection, a runbook; any template runbook for the same pattern removed.",
        ["wontfix"] = "History line; a recurring pattern is proposed as noise instead.",
        ["duplicate"] = "None (the incident links the original).",
    };

    public static bool NeedsRepositoryChange(string? outcome) => outcome is not null && outcome != "duplicate" && RepositoryChange.ContainsKey(outcome);
}

/// <summary>
/// What a person does on the dashboard (operations.md 10.6), also behind the JSON API: acknowledge, close with an outcome, send a draft,
/// investigate again, search, open a manual incident, add a suppression window, and accept an incident from another monitor.
/// </summary>
public sealed class IncidentActions(
    IIncidentStore store, IRunContextProvider contexts, IncidentProcessor processor, NotificationDispatcher notifier,
    SuppressionService suppression, TimeProvider time)
{
    public async Task<ActionResult> AcknowledgeAsync(string id, string user, CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        string? problem = null;
        var row = await store.UpdateAsync(id, now, report =>
        {
            var status = MonitorJson.ScalarText(report["status"]);
            if (status != "open") { problem = $"The incident is {status}; only an open incident can be acknowledged."; return IncidentChange.None; }
            report["status"] = "acknowledged";
            IncidentReports.AddAction(report, now, ReportActions.Acknowledged, user);
            return IncidentChange.Of("status_change", new JsonObject { ["from"] = "open", ["to"] = "acknowledged", ["by"] = user });
        }, ct).ConfigureAwait(false);
        if (row is null) return new ActionResult(false, "No such incident.");
        return problem is null ? new ActionResult(true, "Incident acknowledged.") : new ActionResult(false, problem);
    }

    public async Task<ActionResult> CloseAsync(string id, CloseRequest request, string user, CancellationToken ct = default)
    {
        var masker = contexts.Current.Runtime.Masker;
        if (!Outcomes.All.Contains(request.Value)) return new ActionResult(false, $"The outcome must be one of: {string.Join(", ", Outcomes.All)}.");
        if (Length(request.Ticket) > 500 || Length(request.PullRequest) > 500) return new ActionResult(false, "The ticket and the pull request must be at most 500 characters.");
        if (Length(request.Note) > 1000) return new ActionResult(false, "The note must be at most 1000 characters.");

        var note = Clean(masker, request.Note);
        if (request.Value == "duplicate")
        {
            var original = request.DuplicateOf?.Trim();
            if (string.IsNullOrEmpty(original)) return new ActionResult(false, "A duplicate names the original incident.");
            if (original == id || await store.GetAsync(original, ct).ConfigureAwait(false) is null) return new ActionResult(false, $"The original incident {original} does not exist.");
            note = IncidentReports.Cut($"Duplicate of {original}." + (note is null ? "" : " " + note), 1000);
        }

        var now = time.GetUtcNow();
        string? problem = null;
        var row = await store.UpdateAsync(id, now, report =>
        {
            var status = MonitorJson.ScalarText(report["status"]);
            if (status == "closed") { problem = "The incident is already closed."; return IncidentChange.None; }
            report["status"] = "closed";
            report["outcome"] = new JsonObject
            {
                ["value"] = request.Value,
                ["ticket"] = Clean(masker, request.Ticket),
                ["pull_request"] = Clean(masker, request.PullRequest),
                ["closed_at"] = Iso(now),
                ["closed_by"] = IncidentReports.Cut(user, 200),
                ["note"] = note,
            };
            IncidentReports.AddAction(report, now, ReportActions.Closed, request.Value);
            return IncidentChange.Of("status_change", new JsonObject { ["from"] = status, ["to"] = "closed", ["by"] = user, ["outcome"] = request.Value });
        }, ct).ConfigureAwait(false);
        if (row is null) return new ActionResult(false, "No such incident.");
        return problem is null ? new ActionResult(true, $"Incident closed as {request.Value}.") : new ActionResult(false, problem);
    }

    /// <summary>Records the pull request that carries the repository change of a closed incident (section 15).</summary>
    public async Task<ActionResult> SetPullRequestAsync(string id, string? pullRequest, string user, CancellationToken ct = default)
    {
        var masker = contexts.Current.Runtime.Masker;
        if (string.IsNullOrWhiteSpace(pullRequest) || pullRequest.Length > 500) return new ActionResult(false, "Give the pull request link (at most 500 characters).");
        var now = time.GetUtcNow();
        string? problem = null;
        var row = await store.UpdateAsync(id, now, report =>
        {
            if (report["outcome"] is not JsonObject outcome) { problem = "The incident is not closed."; return IncidentChange.None; }
            outcome["pull_request"] = Clean(masker, pullRequest);
            return IncidentChange.Of("pull_request", new JsonObject { ["by"] = user });
        }, ct).ConfigureAwait(false);
        if (row is null) return new ActionResult(false, "No such incident.");
        return problem is null ? new ActionResult(true, "Pull request recorded.") : new ActionResult(false, problem);
    }

    public Task<SendOutcome> SendDraftAsync(long notificationId, string user, CancellationToken ct = default) =>
        notifier.SendDraftAsync(contexts.Current.Notifications, notificationId, user, ct);

    public async Task<ActionResult> ReinvestigateAsync(string id, string user, CancellationToken ct = default)
    {
        var row = await store.GetAsync(id, ct).ConfigureAwait(false);
        if (row is null) return new ActionResult(false, "No such incident.");
        var queued = await processor.QueueInvestigationAsync(id, $"requested by {user}", time.GetUtcNow(), ct).ConfigureAwait(false);
        return queued
            ? new ActionResult(true, "The incident is queued for investigation.")
            : new ActionResult(false, "No investigator is available: the incident keeps its rule verdict.");
    }

    // ---- search (10.6) ----

    public async Task<ToolEnvelope> SearchTraceAsync(string user, string traceId, CancellationToken ct = default) =>
        await RunToolAsync(user, "logs_by_correlation", new JsonObject { ["trace_id"] = traceId.Trim().ToLowerInvariant() }, ct).ConfigureAwait(false);

    public async Task<ToolEnvelope> SearchEntityAsync(string user, string entity, string id, CancellationToken ct = default) =>
        await RunToolAsync(user, "audit_trail_by_entity", new JsonObject { ["entity"] = entity.Trim(), ["id"] = id.Trim() }, ct).ConfigureAwait(false);

    private Task<ToolEnvelope> RunToolAsync(string user, string tool, JsonObject args, CancellationToken ct) =>
        contexts.Current.Runtime.Executor.ExecuteAsync(new ToolRequest(tool, args, "dashboard:" + user, new IncidentToolSession(null)), ct);

    public Task<IncidentRow> OpenManualAsync(ManualIncidentRequest request, CancellationToken ct = default) =>
        processor.OpenManualAsync(contexts.Current, request, ct);

    // ---- suppression windows from the dashboard (10.4) ----

    public async Task<ActionResult> AddWindowAsync(string name, DateTimeOffset startsAt, DateTimeOffset endsAt, string match, string? reason, string user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100) return new ActionResult(false, "Give the window a name of at most 100 characters.");
        if (string.IsNullOrWhiteSpace(match) || match.Length > 200) return new ActionResult(false, "The match is a pattern over signature or template ids, for example task.* or t-*.");
        if (endsAt <= startsAt) return new ActionResult(false, "The window must end after it starts.");
        var masker = contexts.Current.Runtime.Masker;
        await store.AddSuppressionWindowAsync(new SuppressionWindowRow(0, name.Trim(), startsAt, endsAt, match.Trim(), user, Clean(masker, reason)), ct).ConfigureAwait(false);
        suppression.Invalidate();
        return new ActionResult(true, "Suppression window added.");
    }

    // ---- ingest (10.6, 12.4) ----

    /// <summary>Accepts an incident report from another monitor. Only a schema-valid report is stored, and its text is masked again.</summary>
    public async Task<IngestResult> IngestAsync(JsonObject report, CancellationToken ct = default)
    {
        var ctx = contexts.Current;
        if (!ctx.ReportValidator.IsValid(report, out var errors))
            return new IngestResult(400, "The report does not validate against the incident report schema.", null, errors.Take(10).ToList());

        IncidentReports.MaskAllText(report, ctx.Runtime.Masker);
        var id = MonitorJson.ScalarText(report["id"])!;
        var now = time.GetUtcNow();
        var verdictSource = report["model_verdict"] is JsonObject ? "model" : report["rule_verdict"] is JsonObject ? "rule" : "none";
        if (await store.CreateAsync(report, verdictSource, now, [("ingested", new JsonObject { ["environment"] = MonitorJson.ScalarText(report["environment"]) })], ct).ConfigureAwait(false))
            return new IngestResult(201, "Incident stored.", id, []);

        var existing = await store.GetAsync(id, ct).ConfigureAwait(false);
        var same = existing is not null && existing.Fingerprint == MonitorJson.ScalarText(report["fingerprint"]) && existing.Environment == MonitorJson.ScalarText(report["environment"]);
        if (!same) return new IngestResult(409, $"An incident with the id {id} already exists for another fingerprint or environment.", null, []);

        // the same report again (a retry) or a newer state of it: replaced, so ingest is idempotent
        await store.UpdateAsync(id, now, current =>
        {
            current.Clear();
            foreach (var kv in report) current[kv.Key] = kv.Value?.DeepClone();
            return IncidentChange.Of("ingested", new JsonObject { ["update"] = true });
        }, ct).ConfigureAwait(false);
        return new IngestResult(200, "Incident updated.", id, []);
    }

    private static int Length(string? s) => s?.Length ?? 0;

    private static string? Clean(Masking.Masker masker, string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : masker.MaskFreeText(text.Trim(), 1000);
}
