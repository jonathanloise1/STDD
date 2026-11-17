using MyApp.Application.DTOs.Audit;

namespace MyApp.Application.Interfaces;

/// <summary>
/// Service interface for audit log queries.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Lists the changes of the last year, newest first. <paramref name="to"/> given as a date (midnight)
    /// covers the whole day.
    /// </summary>
    // US-AUD-01, US-AUD-02
    Task<AuditLogPagedResult> GetByOrganizationAsync(Guid userAadId, Guid organizationId,
        DateTime? from, DateTime? to, string? entityType, Guid? userId,
        int page = 1, int pageSize = 50, CancellationToken ct = default);

    /// <summary>The fixed choices of the "Type" field with their friendly names.</summary>
    // US-AUD-02
    Task<IReadOnlyList<AuditEntityTypeDto>> GetEntityTypesAsync(Guid userAadId, Guid organizationId, CancellationToken ct = default);

    // US-AUD-03
    Task<IEnumerable<AuditLogDto>> GetByEntityAsync(Guid userAadId, Guid organizationId,
        string entityType, string entityId, CancellationToken ct = default);

    // US-AUD-03
    Task<IEnumerable<AuditLogDto>> GetByCorrelationIdAsync(Guid userAadId, Guid organizationId,
        string correlationId, CancellationToken ct = default);
}
