using MyApp.Application.Interfaces;
using MyApp.Application.Logging;
using MyApp.WebApi.Filters;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.WebApi.Controllers;

/// <summary>
/// Query endpoints for audit logs (read-only, Admin only).
/// </summary>
[Route("api/organizations/{organizationId}/audit-logs")]
public class AuditController(
    ILogger<AuditController> logger,
    IAuditService auditService) : AuthenticatedControllerBase
{
    /// <summary>
    /// Changes of the last year, newest first. <c>from</c> and <c>to</c> limit by time; a <c>to</c> without a
    /// time of day covers the whole day.
    /// </summary>
    // US-AUD-01, US-AUD-02
    [HttpGet]
    [ServiceUnavailable(AuditTrailLog.Keys.AudListUnavailable)]
    public async Task<IActionResult> List(
        Guid organizationId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId
        });

        logger.LogDebug("Listing audit logs (from={From}, to={To}, entityType={EntityType}, page={Page}, pageSize={PageSize})",
            from, to, entityType, page, pageSize);

        var result = await auditService.GetByOrganizationAsync(
            AuthenticatedUserId, organizationId, from, to, entityType, userId, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>The fixed choices of the "Type" field with their friendly names.</summary>
    // US-AUD-02
    [HttpGet("entity-types")]
    public async Task<IActionResult> EntityTypes(Guid organizationId, CancellationToken ct = default) =>
        Ok(await auditService.GetEntityTypesAsync(AuthenticatedUserId, organizationId, ct));

    // US-AUD-03
    [HttpGet("entity/{entityType}/{entityId}")]
    public async Task<IActionResult> GetByEntity(
        Guid organizationId, string entityType, string entityId,
        CancellationToken ct = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId,
            ["EntityType"] = entityType,
            ["EntityId"] = entityId
        });

        var result = await auditService.GetByEntityAsync(
            AuthenticatedUserId, organizationId, entityType, entityId, ct);
        return Ok(result);
    }

    // US-AUD-03
    [HttpGet("correlation/{correlationId}")]
    public async Task<IActionResult> GetByCorrelation(
        Guid organizationId, string correlationId,
        CancellationToken ct = default)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["AadId"] = AuthenticatedUserId,
            ["OrganizationId"] = organizationId,
            ["CorrelationId"] = correlationId
        });

        var result = await auditService.GetByCorrelationIdAsync(
            AuthenticatedUserId, organizationId, correlationId, ct);
        return Ok(result);
    }
}
