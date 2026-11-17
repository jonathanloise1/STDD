using MyApp.Domain.Entities;

namespace MyApp.Domain.Repositories;

/// <summary>Storage of the documentation gaps (manual.md 7.4).</summary>
// US-HELP-07
public interface IHelpGapsRepository
{
    Task AddAsync(HelpGap gap, CancellationToken cancellationToken = default);

    /// <summary>Records from <c>from</c> (inclusive) to <c>toExclusive</c>, both UTC and optional, oldest first.</summary>
    Task<List<HelpGap>> ListAsync(DateTime? from, DateTime? toExclusive, CancellationToken cancellationToken = default);

    /// <summary>Deletes the records older than the cutoff; returns how many.</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
