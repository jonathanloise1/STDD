using Microsoft.AspNetCore.Authorization;

namespace MyApp.WebApi.Authorization;

/// <userstory ref="US-MENU-02, US-MENU-06, US-TEAM-08" />
public class OrganizationPermissionRequirement(OrganizationPermission permission) : IAuthorizationRequirement
{
    public OrganizationPermission Permission { get; } = permission;
}
