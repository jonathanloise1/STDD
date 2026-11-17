using MyApp.Application.DTOs.Organizations;
using MyApp.Application.DTOs.Users;
using MyApp.Application.Interfaces;
using MyApp.Domain.Constants;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using MyApp.Application.Exceptions;
using MyApp.Application.Logging;

namespace MyApp.Application.Services;

/// <summary>
/// Service for organization management operations.
/// </summary>
// US-ORG-01, US-ORG-02, US-ORG-03, US-ORG-04, US-ORG-05, US-ORG-06, US-ORG-07, US-ONBOARD-02
public class OrganizationsService(
    ILogger<OrganizationsService> logger,
    IOrganizationsRepository organizationsRepository,
    IUsersRepository usersRepository) : IOrganizationsService
{
    public const long LogoMaxBytes = 5 * 1024 * 1024;

    /// <inheritdoc />
    // US-ORG-01
    public async Task<IEnumerable<OrganizationDto>> GetByUserAadIdAsync(
        Guid userAadId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting organizations for user {UserAadId}", userAadId);
        var organizations = await organizationsRepository.GetByUserAadIdAsync(userAadId, cancellationToken);
        logger.LogDebug("Returned {Count} organizations for user {UserAadId}", organizations.Count(), userAadId);
        return organizations.Select(o => o.ToDto());
    }

    /// <inheritdoc />
    // US-ONBOARD-02
    public async Task<OrganizationDto> CreateAsync(
        Guid userAadId, CreateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        var user = await usersRepository.GetByAadIdAsync(userAadId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found.");

        // Update phone number if provided during onboarding
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            user.PhoneNumber = request.PhoneNumber;
            await usersRepository.UpdateAsync(user, cancellationToken);
        }

        var organization = request.ToEntity();
        organization.Id = Guid.NewGuid();

        // Add creator as Admin
        var orgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OrganizationId = organization.Id,
            Email = user.Email,
            FullName = user.GetFullName(),
            Role = OrganizationRole.Admin,
            Status = OrganizationUserStatus.Active
        };

        organization.Users.Add(orgUser);
        await organizationsRepository.AddAsync(organization, cancellationToken);

        logger.LogInformation("Organization {OrgId} created by user {UserId}", organization.Id, userAadId);

        return organization.ToDto();
    }

    /// <inheritdoc />
    // US-ORG-02
    public async Task<OrganizationDto> UpdateAsync(
        Guid organizationId, UpdateOrganizationRequest request, Guid userAadId,
        CancellationToken cancellationToken = default)
    {
        var org = await GetOrganizationAsync(organizationId, cancellationToken);
        RequireAdmin(org, userAadId, "Only admins can update the organization.");

        request.ApplyTo(org);
        await organizationsRepository.UpdateAsync(org, cancellationToken);

        logger.LogInformation("Organization {OrganizationId} updated by user {UserAadId}", organizationId, userAadId);

        return org.ToDto();
    }

    /// <inheritdoc />
    // US-ORG-03
    public async Task<LogoUploadResultDto> UploadLogoAsync(
        Guid userAadId, Guid organizationId, string contentType, long length, Stream content,
        CancellationToken cancellationToken = default)
    {
        var org = await GetOrganizationAsync(organizationId, cancellationToken);
        RequireAdmin(org, userAadId, "Only admins can upload the organization logo.");

        if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException(new ValidationResult("Only image files are allowed", ["file"]), null, contentType);
        if (length > LogoMaxBytes)
            throw new ValidationException(new ValidationResult("Maximum file size is 5MB", ["file"]), null, length);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        org.LogoUrl = $"data:{contentType};base64,{Convert.ToBase64String(buffer.ToArray())}";
        await organizationsRepository.UpdateAsync(org, cancellationToken);

        logger.LogInformation("Logo of organization {OrganizationId} uploaded by user {UserAadId}", organizationId, userAadId);

        return new LogoUploadResultDto { LogoUrl = org.LogoUrl };
    }

    /// <inheritdoc />
    // US-ORG-01
    public async Task<OrganizationDto> GetByIdAsync(
        Guid userAadId, Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting organization {OrganizationId}", id);
        var org = await GetOrganizationAsync(id, cancellationToken);
        RequireAdmin(org, userAadId, "Only admins can view the organization details.");
        return org.ToDto();
    }

    /// <inheritdoc />
    // US-ORG-04
    public async Task<IEnumerable<OrganizationUserDto>> GetOrganizationUsersAsync(
        Guid userAadId, Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var org = await GetOrganizationAsync(organizationId, cancellationToken);
        RequireAdmin(org, userAadId, "Only admins can view the team.");

        var users = org.Users.Where(u => u.Status == OrganizationUserStatus.Active).ToList();
        logger.LogDebug("Returned {Count} active users for organization {OrganizationId}", users.Count, organizationId);
        return users.Select(u => u.ToDto());
    }

    /// <inheritdoc />
    // US-ORG-05
    public async Task InviteCollaboratorAsync(
        Guid userAadId, Guid organizationId, InviteCollaboratorRequest request,
        CancellationToken cancellationToken = default)
    {
        var org = await GetOrganizationAsync(organizationId, cancellationToken);
        RequireAdmin(org, userAadId, "Only admins can invite collaborators.");

        // Check for existing active or pending member with this email
        var existing = org.Users.FirstOrDefault(u =>
            u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase) &&
            u.Status != OrganizationUserStatus.Disabled);
        if (existing is not null)
        {
            if (existing.Status == OrganizationUserStatus.Active)
            {
                logger.OrgInviteAlreadyMember(organizationId);
                throw new ConflictException(OrganizationsLog.Keys.OrgInviteAlreadyMember, OutcomeMessages.AlreadyMember);
            }

            logger.OrgInviteAlreadyInvited(organizationId);
            throw new ConflictException(OrganizationsLog.Keys.OrgInviteAlreadyInvited, OutcomeMessages.AlreadyInvited);
        }

        var orgUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Email = request.Email.ToLower(),
            FullName = request.FullName,
            Role = Enum.TryParse<OrganizationRole>(request.Role, true, out var role) ? role : OrganizationRole.Viewer,
            Status = OrganizationUserStatus.Pending
        };

        await organizationsRepository.AddOrganizationUserAsync(orgUser, cancellationToken);

        logger.LogInformation("Collaborator {MembershipId} invited to organization {OrgId} with role {Role}",
            orgUser.Id, organizationId, orgUser.Role);
    }

    /// <inheritdoc />
    // US-ORG-07
    public async Task RemoveCollaboratorAsync(
        Guid requesterAadId, Guid userId, Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var org = await GetOrganizationAsync(organizationId, cancellationToken);

        var requester = org.Users.FirstOrDefault(u => u.User?.AadId == requesterAadId && u.Status == OrganizationUserStatus.Active);
        if (requester is null || requester.Role != OrganizationRole.Admin)
            throw new UnauthorizedAccessException("Only admins can remove collaborators.");

        var targetUser = org.Users.FirstOrDefault(u => u.User?.Id == userId);
        if (targetUser is null || targetUser.Status == OrganizationUserStatus.Disabled)
        {
            logger.OrgRemoveNotFound(targetUser?.Id ?? Guid.Empty, organizationId);
            throw new ResourceNotFoundException(OrganizationsLog.Keys.OrgRemoveNotFound, OutcomeMessages.MemberNotFound);
        }

        if (targetUser.Id == requester.Id)
            throw new ValidationException("You cannot remove yourself from the organization.");

        targetUser.Status = OrganizationUserStatus.Disabled;
        await organizationsRepository.UpdateAsync(org, cancellationToken);

        logger.LogInformation("Membership {MembershipId} removed from organization {OrgId} by {RequesterId}",
            targetUser.Id, organizationId, requesterAadId);
    }

    /// <inheritdoc />
    // US-ORG-06
    public async Task UpdateOrganizationUsersAsync(
        Guid userAadId, Guid targetUserId, Guid organizationId, UpdateOrganizationUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var org = await GetOrganizationAsync(organizationId, cancellationToken);
        RequireAdmin(org, userAadId, "Only admins can update collaborators.");

        var targetUser = org.Users.FirstOrDefault(u => u.User?.Id == targetUserId);
        if (targetUser is null || targetUser.Status == OrganizationUserStatus.Disabled)
        {
            logger.OrgRoleNotFound(targetUser?.Id ?? Guid.Empty, organizationId);
            throw new ResourceNotFoundException(OrganizationsLog.Keys.OrgRoleNotFound, OutcomeMessages.MemberNotFound);
        }

        if (Enum.TryParse<OrganizationRole>(request.Role, true, out var newRole))
        {
            // An organization always keeps one active Admin
            if (targetUser.Role == OrganizationRole.Admin && newRole != OrganizationRole.Admin
                && !org.Users.Any(u => u.Id != targetUser.Id && u.Role == OrganizationRole.Admin
                                       && u.Status == OrganizationUserStatus.Active))
            {
                logger.OrgRoleLastAdmin(targetUser.Id, organizationId);
                throw new ConflictException(OrganizationsLog.Keys.OrgRoleLastAdmin, OutcomeMessages.LastAdmin);
            }

            targetUser.Role = newRole;
        }

        await organizationsRepository.UpdateAsync(org, cancellationToken);

        logger.LogInformation("Membership {MembershipId} updated in organization {OrgId} by {RequesterId}",
            targetUser.Id, organizationId, userAadId);
    }

    /// <inheritdoc />
    // US-ORG-04
    public async Task<IEnumerable<PendingInvitationDto>> GetPendingInvitationsAsync(
        Guid userAadId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var org = await GetOrganizationAsync(organizationId, cancellationToken);
        RequireAdmin(org, userAadId, "Only admins can view pending invitations.");

        logger.LogDebug("Getting pending invitations for organization {OrganizationId}", organizationId);

        return org.Users
            .Where(u => u.Status == OrganizationUserStatus.Pending)
            .Select(u => new PendingInvitationDto
            {
                Id = u.Id,
                FullName = u.FullName ?? "",
                Email = u.Email,
                CreatedAt = u.CreatedAt,
                Role = u.Role.ToString()
            })
            .ToList();
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private async Task<Organization> GetOrganizationAsync(Guid organizationId, CancellationToken ct) =>
        await organizationsRepository.GetByIdAsync(organizationId, ct)
        ?? throw new KeyNotFoundException("Organization not found.");

    private static OrganizationUser? ActiveMember(Organization org, Guid userAadId) =>
        org.Users.FirstOrDefault(u => u.User?.AadId == userAadId && u.Status == OrganizationUserStatus.Active);

    private static void RequireAdmin(Organization org, Guid userAadId, string message)
    {
        if (ActiveMember(org, userAadId) is not { Role: OrganizationRole.Admin })
            throw new UnauthorizedAccessException(message);
    }
}
