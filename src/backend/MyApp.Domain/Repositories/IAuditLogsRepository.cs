using MyApp.Domain.Entities;

namespace MyApp.Domain.Repositories;

/// <summary>
/// Repository interface for managing audit logs.
/// <userstory ref="US-AUD-01, US-AUD-02, US-AUD-03, US-AUD-04" />
/// </summary>
public interface IAuditLogsRepository
{
    /// <summary>Returns audit logs for an entity, ordered by Timestamp descending.</summary>
    Task<List<AuditLog>> GetByEntityAsync(string entityType, string entityId, CancellationToken cancellationToken = default);

    /// <summary>Returns audit logs for an organization within a time range, with pagination.</summary>
    Task<(List<AuditLog> Items, int TotalCount)> GetByOrganizationIdAsync(
        Guid organizationId,
        DateTime? from,
        DateTime? to,
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
