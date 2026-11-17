using MyApp.Application.DTOs.Organizations;
using MyApp.Application.DTOs.Users;

namespace MyApp.Application.Interfaces;

/// <summary>
/// Service contract for organization management operations.
/// </summary>
public interface IOrganizationsService
{
    // US-ORG-01
    Task<IEnumerable<OrganizationDto>> GetByUserAadIdAsync(Guid userAadId, CancellationToken cancellationToken = default);

    // US-ONBOARD-02
    Task<OrganizationDto> CreateAsync(Guid userAadId, CreateOrganizationRequest request, CancellationToken cancellationToken = default);

    // US-ORG-02
    Task<OrganizationDto> UpdateAsync(Guid organizationId, UpdateOrganizationRequest request, Guid userAadId, CancellationToken cancellationToken = default);

    // US-ORG-03
    Task<LogoUploadResultDto> UploadLogoAsync(Guid userAadId, Guid organizationId, string contentType, long length, Stream content, CancellationToken cancellationToken = default);

    // US-ORG-01
    Task<OrganizationDto> GetByIdAsync(Guid userAadId, Guid id, CancellationToken cancellationToken = default);

    // US-ORG-04
    Task<IEnumerable<OrganizationUserDto>> GetOrganizationUsersAsync(Guid userAadId, Guid organizationId, CancellationToken cancellationToken = default);

    // US-ORG-05
    Task InviteCollaboratorAsync(Guid userAadId, Guid organizationId, InviteCollaboratorRequest request, CancellationToken cancellationToken = default);

    // US-ORG-07
    Task RemoveCollaboratorAsync(Guid requesterAadId, Guid userId, Guid organizationId, CancellationToken cancellationToken = default);

    // US-ORG-06
    Task UpdateOrganizationUsersAsync(Guid userAadId, Guid targetUserId, Guid organizationId, UpdateOrganizationUserRequest request, CancellationToken cancellationToken = default);

    // US-ORG-04
    Task<IEnumerable<PendingInvitationDto>> GetPendingInvitationsAsync(Guid userAadId, Guid organizationId, CancellationToken cancellationToken = default);
}
