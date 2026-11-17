using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using MyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MyApp.Infrastructure.Repositories;

/// <summary>EF Core repository for HelpGap entities.</summary>
// US-HELP-07
public class HelpGapsRepository(MyAppDbContext context) : IHelpGapsRepository
{
    public async Task AddAsync(HelpGap gap, CancellationToken cancellationToken = default)
    {
        context.HelpGaps.Add(gap);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<HelpGap>> ListAsync(DateTime? from, DateTime? toExclusive, CancellationToken cancellationToken = default)
    {
        var query = context.HelpGaps.AsNoTracking().AsQueryable();
        if (from.HasValue)
            query = query.Where(g => g.RecordedAt >= from.Value);
        if (toExclusive.HasValue)
            query = query.Where(g => g.RecordedAt < toExclusive.Value);

        return await query.OrderBy(g => g.RecordedAt).ThenBy(g => g.Id).ToListAsync(cancellationToken);
    }

    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) =>
        context.HelpGaps.Where(g => g.RecordedAt < cutoffUtc).ExecuteDeleteAsync(cancellationToken);
}
