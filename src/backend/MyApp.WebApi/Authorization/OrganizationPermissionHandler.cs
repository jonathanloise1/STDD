using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace MyApp.WebApi.Authorization;

/// <summary>
/// Handles authorization for organization-scoped role requirements.
/// Role hierarchy: Admin > Editor > Viewer.
/// </summary>
public class OrganizationPermissionHandler(
    ILogger<OrganizationPermissionHandler> logger,
    IOrganizationsRepository organizationsRepository
) : AuthorizationHandler<OrganizationPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrganizationPermissionRequirement requirement)
    {
        // Extract user ID from claims
        if (!Guid.TryParse(context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value, out var userId))
        {
            logger.LogWarning("Authorization failed: missing or invalid user ID in claims.");
            return;
        }

        // Extract organizationId from the route (supports both "organizationId" and "orgId" param names)
        if (context.Resource is not HttpContext httpContext)
        {
            logger.LogWarning("Authorization failed: HttpContext not available.");
            return;
        }

        if (!httpContext.Request.RouteValues.TryGetValue("organizationId", out var orgIdObj))
            httpContext.Request.RouteValues.TryGetValue("orgId", out orgIdObj);

        if (!Guid.TryParse(orgIdObj?.ToString(), out var organizationId))
        {
            logger.LogWarning("Authorization failed: missing or invalid organization ID in route.");
            return;
        }

        // Retrieve organization
        var org = await organizationsRepository.GetByIdAsync(organizationId);
        if (org is null)
        {
            logger.LogWarning("Authorization failed: organization {OrgId} not found.", organizationId);
            return;
        }

        // Retrieve user in context of the organization
        var user = org.Users.FirstOrDefault(u => u.User?.AadId == userId && u.Status == OrganizationUserStatus.Active);
        if (user is null)
        {
            logger.LogWarning("Authorization failed: user {UserId} not active in organization {OrgId}.", userId, organizationId);
            return;
        }

        // Role hierarchy check: Admin > Editor > Viewer
        var isAuthorized = requirement.Permission switch
        {
            OrganizationPermission.Viewer => user.Role is OrganizationRole.Admin or OrganizationRole.Editor or OrganizationRole.Viewer,
            OrganizationPermission.Editor => user.Role is OrganizationRole.Admin or OrganizationRole.Editor,
            OrganizationPermission.Admin => user.Role is OrganizationRole.Admin,
            _ => false
        };

        if (isAuthorized)
        {
            logger.LogInformation("Authorization success: user {UserId} with role '{Role}' meets '{Required}' in organization {OrgId}.",
                userId, user.Role, requirement.Permission, organizationId);
            context.Succeed(requirement);
        }
        else
        {
            logger.LogWarning("Authorization denied: user {UserId} with role '{Role}' does not meet '{Required}' in organization {OrgId}.",
                userId, user.Role, requirement.Permission, organizationId);
        }
    }
}
