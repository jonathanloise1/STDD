namespace MyApp.Monitor.Investigator;

/// <summary>States of the investigator host, shown on the Sources page.</summary>
public static class InvestigatorStates
{
    /// <summary>Profile <c>tier: none</c>: rules only by choice.</summary>
    public const string Off = "off";
    /// <summary>A host answers and is compatible with the lock and the pack.</summary>
    public const string Ready = "ready";
    /// <summary>No usable host: rules only, with the reason.</summary>
    public const string RulesOnly = "rules_only";
    /// <summary>Three failed investigations in a row (11.2): rules only until the monitor restarts.</summary>
    public const string Disabled = "disabled";
}

public sealed record InvestigatorStatus(
    string State, string? Tier, string? Device, string? ModelVersion, string Reason, int ConsecutiveFailures = 0, int Completed = 0, int Failed = 0,
    DateTimeOffset? CheckedAt = null)
{
    public bool IsReady => State == InvestigatorStates.Ready;
}

/// <summary>The outcome of the selection of 11.2: the host to use, or rules only with the reason.</summary>
public sealed record InvestigatorSelection(InvestigatorStatus Status, IInferenceBackend? Backend, TierExport? Export)
{
    public bool IsReady => Backend is not null;
}

/// <summary>The inputs of the selection that come from the profile, the options and the pack.</summary>
public sealed record SelectionInput(
    string Tier, string Device, string? LockPath, string PackProtocolSha256, MonitorOptions.InvestigatorOptions Options);

/// <summary>
/// The selection of operations.md 11.2, in order: <c>tier: none</c> is rules only; the lock gives the export, context length and memory of the tier
/// (missing: rules only, <c>monitor.investigator.unavailable</c>); the lock's protocol must be the pack's; then the hosts are asked, the profile's tier
/// first and, for <c>large</c>, <c>small</c> after it. A host is usable when it serves <c>investigator-{lock version}</c> on the device the profile allows
/// (<c>auto</c>: the launcher's own GPU-then-CPU choice, read from the alias) and, on the CPU, when the machine has the memory the export needs.
/// </summary>
public sealed class InvestigatorSelector(IInferenceBackendFactory factory, TimeProvider time, Func<long> totalMemoryMb)
{
    public const string ModelId = "investigator";
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    public async Task<InvestigatorSelection> SelectAsync(SelectionInput input, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        InvestigatorSelection Rules(string state, string reason, string? tier = null) => new(new InvestigatorStatus(state, tier, null, null, reason, CheckedAt: now), null, null);

        if (input.Tier == "none") return Rules(InvestigatorStates.Off, "the profile sets investigator.tier to none: rules only");
        if (input.LockPath is null || !File.Exists(input.LockPath))
            return Rules(InvestigatorStates.RulesOnly, $"the model lock was not found{(input.LockPath is null ? " (set Monitor:Investigator:ModelLock)" : ": " + input.LockPath)}: rules only", input.Tier);
        var lockEntry = InvestigatorLock.TryRead(input.LockPath, out var problem);
        if (lockEntry is null) return Rules(InvestigatorStates.RulesOnly, problem + ": rules only", input.Tier);
        if (!string.Equals(lockEntry.ProtocolSha256, input.PackProtocolSha256, StringComparison.OrdinalIgnoreCase))
            return Rules(InvestigatorStates.RulesOnly, "the protocol the model was trained with is not the protocol of the loaded pack (lock and support.agent.md differ): rules only", input.Tier);

        var order = input.Tier == "large" ? new[] { "large", "small" } : [input.Tier];
        var reasons = new List<string>();
        foreach (var tier in order)
        {
            if (!lockEntry.Tiers.TryGetValue(tier, out var export)) { reasons.Add($"the lock has no {tier} export"); continue; }
            var endpoint = new Uri(input.Options.Endpoints.TryGetValue(tier, out var own) && !string.IsNullOrWhiteSpace(own) ? own : input.Options.Endpoint);
            var backend = factory.Create(tier, endpoint, lockEntry.TemplateKwargs(tier));
            HostInfo info;
            using (var probe = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                probe.CancelAfter(ProbeTimeout);
                try { info = await backend.ProbeAsync(probe.Token).ConfigureAwait(false); }
                catch (HostException ex) { reasons.Add($"{tier}: {ex.Message}"); continue; }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { reasons.Add($"{tier}: the host at {endpoint} did not answer within {ProbeTimeout.TotalSeconds:0} seconds"); continue; }
            }

            if (info.ModelAndVersion != $"{ModelId}-{lockEntry.ModelVersion}")
            {
                reasons.Add($"{tier}: the host serves '{info.Alias}', the lock has {ModelId}-{lockEntry.ModelVersion}");
                continue;
            }
            var device = info.Device;
            if (device is not ("gpu" or "cpu")) { reasons.Add($"{tier}: the host's alias '{info.Alias}' does not say gpu or cpu"); continue; }
            if (input.Device != "auto" && input.Device != device) { reasons.Add($"{tier}: the host runs on the {device}, the profile requires the {input.Device}"); continue; }
            if (device == "cpu" && totalMemoryMb() < export.MemoryMb) { reasons.Add($"{tier}: the {export.MemoryMb} MB the export needs are more than this machine has"); continue; }

            var fallback = tier != input.Tier ? $" (fell back from {input.Tier})" : "";
            return new InvestigatorSelection(
                new InvestigatorStatus(InvestigatorStates.Ready, tier, device, lockEntry.ModelVersion, $"the host answers as {info.Alias}{fallback}", CheckedAt: now), backend, export);
        }
        return Rules(InvestigatorStates.RulesOnly, "no host can answer: " + string.Join("; ", reasons) + ": rules only", input.Tier);
    }

    public static long DefaultTotalMemoryMb() => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
}
