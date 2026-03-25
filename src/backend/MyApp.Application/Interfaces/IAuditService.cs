using MyApp.Application.DTOs.Audit;

namespace MyApp.Application.Interfaces;

/// <summary>
/// Service interface for audit log queries.
/// <userstory ref="US-AUD-02, US-AUD-03, US-AUD-04" />
/// </summary>
public interface IAuditService
{
    /// <userstory ref="US-AUD-02" />
    Task<AuditLogPagedResult> GetByOrganizationAsync(Guid userAadId, Guid organizationId,
        DateTime? from, DateTime? to, string? entityType, Guid? userId,
        int page = 1, int pageSize = 50, CancellationToken ct = default);

    /// <userstory ref="US-AUD-03" />
    Task<IEnumerable<AuditLogDto>> GetByEntityAsync(Guid userAadId, Guid organizationId,
        string entityType, string entityId, CancellationToken ct = default);

    /// <userstory ref="US-AUD-04" />
    Task<IEnumerable<AuditLogDto>> GetByCorrelationIdAsync(Guid userAadId, Guid organizationId,
        string correlationId, CancellationToken ct = default);
}
