using MyApp.Application.DTOs.Organizations;
using MyApp.Application.DTOs.Users;

namespace MyApp.Application.Interfaces;

/// <summary>
/// Service contract for organization management operations.
/// </summary>
public interface IOrganizationsService
{
    Task<IEnumerable<OrganizationDto>> GetByUserAadIdAsync(Guid userAadId, CancellationToken cancellationToken = default);
    Task<OrganizationDto> CreateAsync(Guid userAadId, CreateOrganizationRequest request, CancellationToken cancellationToken = default);
    Task<OrganizationDto> UpdateAsync(Guid organizationId, UpdateOrganizationRequest request, Guid userAadId, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(Guid userAadId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<OrganizationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrganizationUserDto>> GetOrganizationUsersAsync(Guid userAadId, Guid organizationId, CancellationToken cancellationToken = default);
    Task InviteCollaboratorAsync(Guid userAadId, Guid organizationId, InviteCollaboratorRequest request, CancellationToken cancellationToken = default);
    Task RemoveCollaboratorAsync(Guid requesterId, Guid userId, Guid organizationId, CancellationToken cancellationToken = default);
    Task UpdateOrganizationUsersAsync(Guid userAadId, Guid targetUserId, Guid organizationId, UpdateOrganizationUserRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<PendingInvitationDto>> GetPendingInvitationsAsync(Guid userAadId, Guid organizationId, CancellationToken cancellationToken = default);
}
