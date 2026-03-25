using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using MyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for AuditLog entities.
/// <userstory ref="US-AUD-01, US-AUD-02, US-AUD-03, US-AUD-04" />
/// </summary>
public class AuditLogsRepository(
    ILogger<AuditLogsRepository> logger,
    MyAppDbContext context) : IAuditLogsRepository
{
    public async Task<List<AuditLog>> GetByEntityAsync(
        string entityType, string entityId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Listing audit logs for entity {EntityType}/{EntityId}", entityType, entityId);
        return await context.AuditLogs
            .Include(a => a.User)
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task<(List<AuditLog> Items, int TotalCount)> GetByOrganizationIdAsync(
        Guid organizationId,
        DateTime? from,
        DateTime? to,
        string? entityType,
        Guid? userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Querying audit logs for organization {OrgId} (page {Page}, size {Size})",
            organizationId, page, pageSize);

        var query = context.AuditLogs
            .Include(a => a.User)
            .Where(a => a.OrganizationId == organizationId);

        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.Timestamp <= to.Value);
        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(a => a.EntityType == entityType);
        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<List<AuditLog>> GetByCorrelationIdAsync(
        string correlationId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Listing audit logs for correlation {CorrelationId}", correlationId);
        return await context.AuditLogs
            .Include(a => a.User)
            .Where(a => a.CorrelationId == correlationId)
            .OrderBy(a => a.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Adding audit log entry for {EntityType}/{EntityId} ({Action})",
            auditLog.EntityType, auditLog.EntityId, auditLog.Action);
        await context.AuditLogs.AddAsync(auditLog, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<AuditLog> auditLogs, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Bulk adding audit log entries");
        await context.AuditLogs.AddRangeAsync(auditLogs, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
