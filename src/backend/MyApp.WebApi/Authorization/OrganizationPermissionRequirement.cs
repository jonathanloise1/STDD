using Microsoft.AspNetCore.Authorization;

namespace MyApp.WebApi.Authorization;

// US-AUTH-07
public class OrganizationPermissionRequirement(OrganizationPermission permission) : IAuthorizationRequirement
{
    public OrganizationPermission Permission { get; } = permission;
}
