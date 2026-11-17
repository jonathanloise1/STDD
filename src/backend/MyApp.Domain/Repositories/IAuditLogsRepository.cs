using MyApp.Domain.Entities;

namespace MyApp.Domain.Repositories;

/// <summary>
/// Repository interface for managing audit logs.
/// </summary>
// US-AUD-01, US-AUD-02, US-AUD-03
public interface IAuditLogsRepository
{
    /// <summary>Returns audit logs for an entity, ordered by Timestamp descending.</summary>
    Task<List<AuditLog>> GetByEntityAsync(string entityType, string entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns audit logs for an organization with pagination, newest first. The time range is
    /// <c>notBefore</c> and <c>from</c> (inclusive) to <c>toExclusive</c>; all UTC.
    /// </summary>
    Task<(List<AuditLog> Items, int TotalCount)> GetByOrganizationIdAsync(
        Guid organizationId,
        DateTime notBefore,
        DateTime? from,
        DateTime? toExclusive,
        string? entityType,
        Guid? userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Returns audit logs with the same correlation ID.</summary>
    Task<List<AuditLog>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);

    /// <summary>Adds a new audit log entry.</summary>
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    /// <summary>Bulk-adds audit log entries (used by interceptor for multi-entity changes).</summary>
    Task AddRangeAsync(IEnumerable<AuditLog> auditLogs, CancellationToken cancellationToken = default);
}
