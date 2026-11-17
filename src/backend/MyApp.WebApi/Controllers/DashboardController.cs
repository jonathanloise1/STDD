using Microsoft.AspNetCore.Mvc;
using MyApp.Application.Interfaces;
using MyApp.Application.Logging;
using MyApp.WebApi.Filters;

namespace MyApp.WebApi.Controllers;

/// <summary>Task figures of the Dashboard page.</summary>
[Route("api/organizations/{organizationId}/dashboard")]
public class DashboardController(IDashboardService dashboardService) : AuthenticatedControllerBase
{
    /// <summary>The counts over every task of the organization.</summary>
    // US-DASH-01
    [HttpGet("counts")]
    [ServiceUnavailable(DashboardLog.Keys.DashKpiUnavailable)]
    public async Task<IActionResult> GetCounts(Guid organizationId, CancellationToken ct = default) =>
        Ok(await dashboardService.GetCountsAsync(AuthenticatedUserId, organizationId, ct));

    /// <summary>The five most recently created tasks.</summary>
    // US-DASH-02
    [HttpGet("recent-tasks")]
    [ServiceUnavailable(DashboardLog.Keys.DashRecentUnavailable)]
    public async Task<IActionResult> GetRecentTasks(Guid organizationId, CancellationToken ct = default) =>
        Ok(await dashboardService.GetRecentTasksAsync(AuthenticatedUserId, organizationId, ct));
}
