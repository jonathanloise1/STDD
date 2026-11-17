using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MyApp.Monitor.Tools;

namespace MyApp.Monitor.Investigator;

/// <summary>A tool call of an investigation with the envelope the executor returned (masked: the model saw exactly this).</summary>
public sealed record InvestigationCall(string Tool, JsonObject Arguments, ToolEnvelope Envelope);

public enum InvestigationOutcome { Reported, Failed }

/// <param name="Report">The report after validation and normalisation, when <see cref="Outcome"/> is <see cref="InvestigationOutcome.Reported"/>.</param>
/// <param name="FailureKind"><c>wall_time</c>, <c>host</c> or <c>invalid_report</c> when failed.</param>
public sealed record InvestigationResult(
    InvestigationOutcome Outcome, JsonObject? Report, IReadOnlyList<InvestigationCall> Calls, bool ValidFirstTime, bool Repaired, bool BudgetExhausted,
    TimeSpan Duration, string? Error, string? FailureKind, IReadOnlySet<string> ReceivedEvidence);

/// <summary>Everything one investigation is given (operations.md 11.3).</summary>
public sealed class InvestigationInput
{
    public required Protocol Protocol { get; init; }
    public required string System { get; init; }
    public required JsonNode Incident { get; init; }
    public JsonNode? Pack { get; init; }
    public required IReadOnlyList<(string Name, string Description, JsonNode Parameters)> Tools { get; init; }
    public required IReadOnlyList<(string Name, string Description)> Entities { get; init; }
    public required IReadOnlyList<(string Id, string Text)> Constraints { get; init; }
    public required JsonObject InvestigationSchema { get; init; }
    public required int Budget { get; init; }
    public required TimeSpan Timeout { get; init; }
    /// <summary>The context length of the export; above 75 % of it the oldest results are summarised.</summary>
    public int? ContextTokens { get; init; }
    /// <summary>The stories of the pack (the signature's), or null without a pack.</summary>
    public IReadOnlySet<string>? Stories { get; init; }
    public string Caller { get; init; } = "investigator";
    /// <summary><c>user</c> or <c>tool</c>: the role the result turns are sent with (the lock's <c>tool_role</c>).</summary>
    public string ToolRole { get; init; } = Protocol.ProtocolToolRole;
}

/// <summary>
/// The loop of operations.md 11.3, a C# port of <c>training/investigator/host.py</c>: system and user turn, then one call or one report per assistant
/// turn under the grammar, results through the executor (validation, budgets, masking, audit), the stop rules, the validation after the report and one
/// repair turn. A failure, a timeout or an invalid report ends it as <see cref="InvestigationOutcome.Failed"/>; it never throws for those.
/// </summary>
public sealed class InvestigationLoop(IInferenceBackend backend, ToolExecutor executor, TimeProvider time)
{
    private static readonly Regex ResultTurn = new(@"^<result>(.*)</result>\n(<budget calls=""\d+"" />)$", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public async Task<InvestigationResult> RunAsync(InvestigationInput input, IncidentToolSession session, CancellationToken ct)
    {
        var protocol = input.Protocol;
        var validator = new SchemaSubsetValidator(input.InvestigationSchema);
        var entityNames = input.Entities.Select(e => e.Name).ToList();
        var messages = new List<ChatMessage>
        {
            new("system", input.System),
            new("user", protocol.RenderUser(input.Incident, input.Pack, input.Tools, input.Entities, input.Constraints, input.Budget)),
        };
        var started = time.GetUtcNow();
        var calls = new List<InvestigationCall>();
        var evidence = new HashSet<string>(StringComparer.Ordinal) { "E1" };
        var callsLeft = input.Budget;
        var consecutiveFailures = 0;
        var duplicates = 0;
        var forceReport = false;
        var repairs = 0;
        var budgetExhausted = false;
        var grammars = new Dictionary<bool, string>();

        InvestigationResult Done(JsonObject? report, bool firstTime, bool repaired, string? error, string? kind) => new(
            report is null ? InvestigationOutcome.Failed : InvestigationOutcome.Reported, report, calls, firstTime, repaired, budgetExhausted, time.GetUtcNow() - started, error, kind, evidence);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var left = input.Timeout - (time.GetUtcNow() - started);
            if (left <= TimeSpan.Zero) return Done(null, false, repairs > 0, "wall time reached", "wall_time");

            var onlyReport = forceReport || callsLeft <= 0;
            if (!grammars.TryGetValue(onlyReport, out var grammar))
                grammars[onlyReport] = grammar = TurnGrammar.Build(input.Tools.Select(t => (t.Name, t.Parameters)), input.InvestigationSchema, entityNames, onlyReport);
            var view = input.ContextTokens is { } context ? SummariseOldResults(messages, protocol.ResultRole, context, protocol.SummaryFraction) : messages;

            string text;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cts.CancelAfter(left);
                try { text = await backend.GenerateAsync(new GenerationRequest(WireView(view, protocol, input.ToolRole), protocol.MaxNewTokens, grammar), cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Done(null, false, repairs > 0, "wall time reached", "wall_time"); }
                catch (HostException ex) { return Done(null, false, repairs > 0, ex.Message, "host"); }
            }

            var turn = Protocol.ParseTurn(text);
            messages.Add(new ChatMessage("assistant", text));

            if (turn.Kind == Turn.Call && !onlyReport)
            {
                var tool = MonitorJson.ScalarText(turn.Payload!["tool"])!;
                var arguments = (JsonObject)turn.Payload["arguments"]!.DeepClone();
                var envelope = await executor.ExecuteAsync(new ToolRequest(tool, arguments, input.Caller, session), ct).ConfigureAwait(false);
                callsLeft--;
                if (envelope.IsSuccess && envelope.EvidenceId is not null) evidence.Add(envelope.EvidenceId);
                else if (envelope.Status == ToolStatus.Duplicate) duplicates++;
                consecutiveFailures = Protocol.FailedStatuses.Contains(envelope.Status) ? consecutiveFailures + 1 : 0;
                calls.Add(new InvestigationCall(tool, arguments, envelope));
                messages.Add(new ChatMessage(protocol.ResultRole, protocol.RenderResult(envelope.ToJson(), callsLeft)));
                if (callsLeft <= 0) budgetExhausted = true;
                forceReport = callsLeft <= 0 || consecutiveFailures >= protocol.ConsecutiveFailures || duplicates >= protocol.DuplicateLimit;
                continue;
            }

            // a report, or something that is not a valid turn (which counts as an invalid report)
            List<string> errors;
            if (turn.Kind == Turn.Call) errors = ["the next turn must be the report"];
            else if (turn.Kind != Turn.Report) errors = [turn.Error ?? "not a report"];
            else errors = Protocol.ValidateReport(validator, turn.Payload, evidence, input.Stories);
            if (errors.Count == 0) return Done(protocol.NormaliseReport(turn.Payload!), repairs == 0, repairs > 0, null, null);

            if (repairs >= protocol.RepairTurns)
            {
                var joined = string.Join("; ", errors);
                return Done(null, false, true, "invalid report after the repair turn: " + (joined.Length > 200 ? joined[..200] : joined), "invalid_report");
            }
            repairs++;
            forceReport = true;
            messages.Add(new ChatMessage("user", protocol.RenderRepair(errors)));
        }
    }

    /// <summary>The messages as the host receives them: the conversation keeps the protocol's roles, only the wire role of the results changes.</summary>
    public static IReadOnlyList<ChatMessage> WireView(IReadOnlyList<ChatMessage> messages, Protocol protocol, string? toolRole) =>
        toolRole == "user" ? messages.Select(m => m with { Role = protocol.WireRole(m.Role, toolRole) }).ToList() : messages;

    /// <summary>Replaces the oldest results by their summary until the conversation is within <paramref name="fraction"/> of the context (11.3).</summary>
    public static List<ChatMessage> SummariseOldResults(IReadOnlyList<ChatMessage> messages, string resultRole, int contextTokens, double fraction)
    {
        var result = messages.ToList();
        var total = result.Sum(m => Tokens(m.Content));
        for (var i = 0; i < result.Count; i++)
        {
            if (total <= fraction * contextTokens) break;
            var m = result[i];
            if (m.Role != resultRole || m.Content.Contains("summarised", StringComparison.Ordinal)) continue;
            var match = ResultTurn.Match(m.Content);
            if (!match.Success) continue;
            var env = JsonNode.Parse(match.Groups[1].Value);
            var summary = new JsonObject { ["evidence_id"] = env?["evidence_id"]?.DeepClone(), ["summarised"] = true, ["rows"] = env?["rows"]?.DeepClone() ?? 0 };
            var replaced = $"<result>{ProtocolJson.Dumps(summary)}</result>\n{match.Groups[2].Value}";
            total += Tokens(replaced) - Tokens(m.Content);
            result[i] = m with { Content = replaced };
        }
        return result;
    }

    private static int Tokens(string text) => (int)Math.Round(text.Length / 3.5, MidpointRounding.ToEven);
}
