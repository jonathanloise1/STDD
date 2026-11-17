using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;

namespace MyApp.Application.Services;

/// <summary>Checks shared by the services that act on one organization.</summary>
internal static class MembershipChecks
{
    /// <summary>
    /// Returns the active membership of the user in the organization.
    /// Throws <see cref="KeyNotFoundException"/> when the organization does not exist and
    /// <see cref="UnauthorizedAccessException"/> when the user is not an active member.
    /// </summary>
    public static async Task<OrganizationUser> EnsureActiveMemberAsync(
        IOrganizationsRepository organizations, Guid userAadId, Guid organizationId, CancellationToken ct)
    {
        var org = await organizations.GetByIdAsync(organizationId, ct)
            ?? throw new KeyNotFoundException($"Organization {organizationId} not found.");

        return org.Users.FirstOrDefault(u => u.User != null && u.User.AadId == userAadId
                   && u.Status == OrganizationUserStatus.Active)
               ?? throw new UnauthorizedAccessException("User is not an active member of this organization.");
    }
}
