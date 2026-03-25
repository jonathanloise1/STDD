using MyApp.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.WebApi.Controllers;

/// <summary>
/// Query endpoints for audit logs (read-only, Admin only).
/// <userstory ref="US-AUD-01, US-AUD-02" />
/// </summary>
[Route("api/organizations/{organizationId}/audit-logs")]
public class AuditController(
    ILogger<AuditController> logger,
    IAuditService auditService) : AuthenticatedControllerBase
{
    /// <userstory ref="US-AUD-01" />
    [HttpGet]
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

        logger.LogDebug("Listing audit logs (from={From}, to={To}, entityType={EntityType}, userId={UserId}, page={Page}, pageSize={PageSize})",
            from, to, entityType, userId, page, pageSize);

        try
        {
            var result = await auditService.GetByOrganizationAsync(
                AuthenticatedUserId, organizationId, from, to, entityType, userId, page, pageSize, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized access: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("List audit logs failed: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error listing audit logs");
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }

    /// <userstory ref="US-AUD-02" />
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

        logger.LogDebug("Getting audit logs by entity");

        try
        {
            var result = await auditService.GetByEntityAsync(
                AuthenticatedUserId, organizationId, entityType, entityId, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized access: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Get by entity failed: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error getting audit logs by entity");
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }

    /// <userstory ref="US-AUD-02" />
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

        logger.LogDebug("Getting audit logs by correlation ID");

        try
        {
            var result = await auditService.GetByCorrelationIdAsync(
                AuthenticatedUserId, organizationId, correlationId, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("Unauthorized access: {Message}", ex.Message);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Get by correlation failed: {Message}", ex.Message);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error getting audit logs by correlation");
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }
}
