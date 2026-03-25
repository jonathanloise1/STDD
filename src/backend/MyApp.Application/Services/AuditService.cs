using MyApp.Application.DTOs.Audit;
using MyApp.Application.Interfaces;
using MyApp.Domain.Constants;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace MyApp.Application.Services;

/// <summary>
/// Query-only service for audit log access. Writing is handled by AuditInterceptor.
/// <userstory ref="US-AUD-02, US-AUD-03, US-AUD-04" />
/// </summary>
public class AuditService(
    ILogger<AuditService> logger,
    IAuditLogsRepository auditLogsRepository,
    IOrganizationsRepository organizationsRepository) : IAuditService
{
    /// <inheritdoc />
    /// <userstory ref="US-AUD-02" />
    public async Task<AuditLogPagedResult> GetByOrganizationAsync(
        Guid userAadId, Guid organizationId,
        DateTime? from, DateTime? to, string? entityType, Guid? userId,
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var (_, member) = await ValidateOrgMembershipAsync(userAadId, organizationId, ct);
        if (member.Role != OrganizationRole.Admin)
            throw new UnauthorizedAccessException("Only Admin can view audit logs.");

        var (items, totalCount) = await auditLogsRepository.GetByOrganizationIdAsync(
            organizationId, from, to, entityType, userId, page, pageSize, ct);

        logger.LogInformation("[US-AUD-02] Audit log queried for org {OrgId}: {Count} results (page {Page}/{TotalPages})",
            organizationId, totalCount, page, (int)Math.Ceiling((double)totalCount / pageSize));

        return new AuditLogPagedResult
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    /// <userstory ref="US-AUD-03" />
    public async Task<IEnumerable<AuditLogDto>> GetByEntityAsync(
        Guid userAadId, Guid organizationId,
        string entityType, string entityId, CancellationToken ct = default)
    {
        logger.LogDebug("Getting audit logs for entity {EntityType}/{EntityId}", entityType, entityId);

        var (_, member) = await ValidateOrgMembershipAsync(userAadId, organizationId, ct);
        if (member.Role != OrganizationRole.Admin)
            throw new UnauthorizedAccessException("Only Admin can view audit logs.");

        var logs = await auditLogsRepository.GetByEntityAsync(entityType, entityId, ct);

        // Filter to only this organization's logs
        return logs
            .Where(l => l.OrganizationId == organizationId)
            .Select(MapToDto);
    }

    /// <inheritdoc />
    /// <userstory ref="US-AUD-04" />
    public async Task<IEnumerable<AuditLogDto>> GetByCorrelationIdAsync(
        Guid userAadId, Guid organizationId,
        string correlationId, CancellationToken ct = default)
    {
        logger.LogDebug("Getting audit logs for correlation {CorrelationId}", correlationId);

        var (_, member) = await ValidateOrgMembershipAsync(userAadId, organizationId, ct);
        if (member.Role != OrganizationRole.Admin)
            throw new UnauthorizedAccessException("Only Admin can view audit logs.");

        var logs = await auditLogsRepository.GetByCorrelationIdAsync(correlationId, ct);

        return logs
            .Where(l => l.OrganizationId == organizationId)
            .Select(MapToDto);
    }

    // ── Helpers ──

    private async Task<(Organization org, OrganizationUser member)> ValidateOrgMembershipAsync(
        Guid userAadId, Guid organizationId, CancellationToken ct)
    {
        var org = await organizationsRepository.GetByIdAsync(organizationId, ct)
            ?? throw new InvalidOperationException("Organization not found.");
        var member = org.Users.FirstOrDefault(u => u.User?.AadId == userAadId)
            ?? throw new UnauthorizedAccessException("User is not a member of this organization.");
        return (org, member);
    }

    // ── Mapping ──

    private static AuditLogDto MapToDto(AuditLog a) => new()
    {
        Id = a.Id,
        EntityType = a.EntityType,
        EntityId = a.EntityId,
        Action = a.Action.ToString(),
        UserId = a.UserId,
        UserDisplayName = a.UserDisplayName,
        OldValues = a.OldValues,
        NewValues = a.NewValues,
        ChangedProperties = a.ChangedProperties,
        Timestamp = a.Timestamp,
        CorrelationId = a.CorrelationId
    };
}
