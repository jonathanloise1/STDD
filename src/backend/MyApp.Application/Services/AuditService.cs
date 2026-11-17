using MyApp.Application.DTOs.Audit;
using MyApp.Application.Interfaces;
using MyApp.Domain.Constants;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace MyApp.Application.Services;

/// <summary>
/// Query-only service for audit log access. Writing is handled by AuditInterceptor.
/// </summary>
// US-AUD-01, US-AUD-02, US-AUD-03
public class AuditService(
    ILogger<AuditService> logger,
    IAuditLogsRepository auditLogsRepository,
    IOrganizationsRepository organizationsRepository,
    TimeProvider timeProvider) : IAuditService
{
    /// <summary>Changes older than this are not listed.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(365);

    /// <inheritdoc />
    // US-AUD-01, US-AUD-02
    public async Task<AuditLogPagedResult> GetByOrganizationAsync(
        Guid userAadId, Guid organizationId,
        DateTime? from, DateTime? to, string? entityType, Guid? userId,
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var (_, member) = await ValidateOrgMembershipAsync(userAadId, organizationId, ct);
        if (member.Role != OrganizationRole.Admin)
            throw new UnauthorizedAccessException("Only Admin can view audit logs.");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        // The one-year window is part of the query; a "To" without time of day covers the whole day
        var notBefore = timeProvider.GetUtcNow().UtcDateTime - Window;
        var fromUtc = from.HasValue ? ToUtc(from.Value) : (DateTime?)null;
        DateTime? toExclusive = null;
        if (to.HasValue)
        {
            var toUtc = ToUtc(to.Value);
            toExclusive = toUtc.TimeOfDay == TimeSpan.Zero ? toUtc.AddDays(1) : toUtc.AddTicks(1);
        }

        var (items, totalCount) = await auditLogsRepository.GetByOrganizationIdAsync(
            organizationId, notBefore, fromUtc, toExclusive, entityType, userId, page, pageSize, ct);

        logger.LogInformation("Audit log queried for org {OrgId}: {Count} results (page {Page}/{TotalPages})",
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
    // US-AUD-02
    public async Task<IReadOnlyList<AuditEntityTypeDto>> GetEntityTypesAsync(
        Guid userAadId, Guid organizationId, CancellationToken ct = default)
    {
        var (_, member) = await ValidateOrgMembershipAsync(userAadId, organizationId, ct);
        if (member.Role != OrganizationRole.Admin)
            throw new UnauthorizedAccessException("Only Admin can view audit logs.");

        return AuditEntityTypes.All.Select(t => new AuditEntityTypeDto { Value = t.Value, Name = t.Name }).ToList();
    }

    /// <inheritdoc />
    // US-AUD-03
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
    // US-AUD-03
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
            ?? throw new KeyNotFoundException("Organization not found.");
        var member = org.Users.FirstOrDefault(u => u.User?.AadId == userAadId)
            ?? throw new UnauthorizedAccessException("User is not a member of this organization.");
        return (org, member);
    }

    // A value without a time zone (a date from the filter) is read as UTC
    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    // ── Mapping ──

    private static AuditLogDto MapToDto(AuditLog a) => new()
    {
        Id = a.Id,
        EntityType = a.EntityType,
        EntityTypeName = AuditEntityTypes.NameOf(a.EntityType),
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
