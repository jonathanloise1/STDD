namespace MyApp.Monitor.Investigator;

/// <summary>An incident for the investigator host: it was written first, with its rule data (operations.md 6.8, 11.1).</summary>
public sealed record InvestigationRequest(string IncidentId, string Reason);

/// <summary>
/// The only hook of the investigator (operations.md section 11). <c>run</c> registers <see cref="InvestigatorHost"/>; the other commands keep
/// <see cref="NoOpInvestigator"/>. An incident the rule engine marks residual keeps its rule verdict with <c>model</c> empty and its investigation row
/// <c>skipped</c> whenever no host is available.
/// </summary>
public interface IInvestigator
{
    /// <summary>False when the host is off, missing or failed: the monitor then runs on rules only (operations.md 2).</summary>
    bool IsAvailable { get; }

    /// <summary>What the Sources page shows: the tier and device in use, or why the monitor runs on rules only.</summary>
    InvestigatorStatus Status => new(IsAvailable ? InvestigatorStates.Ready : InvestigatorStates.RulesOnly, null, null, null, IsAvailable ? "available" : "no investigator is registered");

    /// <summary>Queues a residual incident. The host later writes <c>model_verdict</c>, <c>investigation</c>, <c>reproduction</c> and <c>model</c> into the stored report.</summary>
    Task QueueAsync(InvestigationRequest request, CancellationToken ct);
}

public sealed class NoOpInvestigator : IInvestigator
{
    public bool IsAvailable => false;
    public Task QueueAsync(InvestigationRequest request, CancellationToken ct) => Task.CompletedTask;
}
