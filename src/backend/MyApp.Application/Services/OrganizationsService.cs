using AutoMapper;
using MyApp.Application.DTOs.Organizations;
using MyApp.Application.DTOs.Users;
using MyApp.Application.Interfaces;
using MyApp.Domain.Constants;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace MyApp.Application.Services;

/// <summary>
/// Service for organization management operations.
/// </summary>
public class OrganizationsService(
    ILogger<OrganizationsService> logger,
    IOrganizationsRepository organizationsRepository,
    IUsersRepository usersRepository,
    IMapper mapper) : IOrganizationsService
{
    public async Task<IEnumerable<OrganizationDto>> GetByUserAadIdAsync(
        Guid userAadId, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting organizations for user {UserAadId}", userAadId);
        var organizations = await organizationsRepository.GetByUserAadIdAsync(userAadId, cancellationToken);
        logger.LogDebug("Returned {Count} organizations for user {UserAadId}", organizations.Count(), userAadId);
        return mapper.Map<IEnumerable<OrganizationDto>>(organizations);
    }

    public async Task<OrganizationDto> CreateAsync(
        Guid userAadId, CreateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        var user = await usersRepository.GetByAadIdAsync(userAadId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        // Update phone number if provided during onboarding
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            user.PhoneNumber = request.PhoneNumber;
            await usersRepository.UpdateAsync(user, cancellationToken);
        }

        var organization = mapper.Map<Organization>(request);
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

        return mapper.Map<OrganizationDto>(organization);
    }

    public async Task<OrganizationDto> UpdateAsync(
        Guid organizationId, UpdateOrganizationRequest request, Guid userAadId,
        CancellationToken cancellationToken = default)
    {
        var org = await organizationsRepository.GetByIdAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException("Organization not found.");

        var user = org.Users.FirstOrDefault(u => u.User?.AadId == userAadId);
        if (user is null || user.Role != OrganizationRole.Admin)
            throw new UnauthorizedAccessException("Only admins can update the organization.");

        mapper.Map(request, org);
        await organizationsRepository.UpdateAsync(org, cancellationToken);

        logger.LogInformation("Organization {OrganizationId} updated by user {UserAadId}", organizationId, userAadId);

        return mapper.Map<OrganizationDto>(org);
    }

    public async Task SoftDeleteAsync(
        Guid userAadId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var org = await organizationsRepository.GetByIdAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException("Organization not found.");

        var user = org.Users.FirstOrDefault(u => u.User?.AadId == userAadId);
        if (user is null || user.Role != OrganizationRole.Admin)
            throw new UnauthorizedAccessException("Only admins can delete the organization.");

        org.IsDeleted = true;
        await organizationsRepository.UpdateAsync(org, cancellationToken);

        logger.LogInformation("Organization {OrganizationId} soft-deleted by user {UserAadId}", organizationId, userAadId);
    }

    public async Task<OrganizationDto?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Getting organization {OrganizationId}", id);
        var org = await organizationsRepository.GetByIdAsync(id, cancellationToken);
        return org is null ? null : mapper.Map<OrganizationDto>(org);
    }

    public async Task<IEnumerable<OrganizationUserDto>> GetOrganizationUsersAsync(
        Guid userAadId, Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var org = await organizationsRepository.GetByIdAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException("Organization not found.");

        var requestingUser = org.Users.FirstOrDefault(u => u.User?.AadId == userAadId);
        if (requestingUser is null)
            throw new UnauthorizedAccessException("You are not a member of this organization.");

        var users = org.Users.Where(u => u.Status == OrganizationUserStatus.Active);
        logger.LogDebug("Returned {Count} active users for organization {OrganizationId}", users.Count(), organizationId);
        return mapper.Map<IEnumerable<OrganizationUserDto>>(users);
    }

    public async Task InviteCollaboratorAsync(
        Guid userAadId, Guid organizationId, InviteCollaboratorRequest request,
        CancellationToken cancellationToken = default)
    {
        var org = await organizationsRepository.GetByIdAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException("Organization not found.");

        var requestingUser = org.Users.FirstOrDefault(u => u.User?.AadId == userAadId);
        if (requestingUser is null || requestingUser.Role != OrganizationRole.Admin)
            throw new UnauthorizedAccessException("Only admins can invite collaborators.");

        // Check for existing active or pending member with this email
        var existing = org.Users.FirstOrDefault(u =>
            u.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase) &&
            u.Status != OrganizationUserStatus.Disabled);
        if (existing is not null)
            throw new InvalidOperationException(
                existing.Status == OrganizationUserStatus.Active
                    ? "User is already a member of this organization."
                    : "A pending invitation for this email already exists.");

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

        logger.LogInformation("Collaborator invited: {Email} to organization {OrgId} with role {Role}",
            request.Email, organizationId, orgUser.Role);
    }

    public async Task RemoveCollaboratorAsync(
        Guid requesterId, Guid userId, Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var org = await organizationsRepository.GetByIdAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException("Organization not found.");

        var requester = org.Users.FirstOrDefault(u => u.User?.Id == requesterId);
        if (requester is null || requester.Role != OrganizationRole.Admin)
            throw new UnauthorizedAccessException("Only admins can remove collaborators.");

        var targetUser = org.Users.FirstOrDefault(u => u.User?.Id == userId);
        if (targetUser is null)
            throw new InvalidOperationException("User not found in this organization.");

        if (targetUser.Id == requester.Id)
            throw new InvalidOperationException("You cannot remove yourself from the organization.");

        targetUser.Status = OrganizationUserStatus.Disabled;
        await organizationsRepository.UpdateAsync(org, cancellationToken);

        logger.LogInformation("User {TargetUserId} removed from organization {OrgId} by {RequesterId}",
            userId, organizationId, requesterId);
    }

    public async Task UpdateOrganizationUsersAsync(
        Guid userAadId, Guid targetUserId, Guid organizationId, UpdateOrganizationUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var org = await organizationsRepository.GetByIdAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException("Organization not found.");

        var requester = org.Users.FirstOrDefault(u => u.User?.AadId == userAadId);
        if (requester is null || requester.Role != OrganizationRole.Admin)
            throw new UnauthorizedAccessException("Only admins can update collaborators.");

        var targetUser = org.Users.FirstOrDefault(u => u.User?.Id == targetUserId);
        if (targetUser is null)
            throw new InvalidOperationException("User not found in this organization.");

        // Update role
        if (Enum.TryParse<OrganizationRole>(request.Role, true, out var newRole))
        {
            targetUser.Role = newRole;
        }

        await organizationsRepository.UpdateAsync(org, cancellationToken);

        logger.LogInformation("User {TargetUserId} updated in organization {OrgId} by {RequesterId}",
            targetUserId, organizationId, userAadId);
    }

    public async Task<IEnumerable<PendingInvitationDto>> GetPendingInvitationsAsync(
        Guid userAadId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var org = await organizationsRepository.GetByIdAsync(organizationId, cancellationToken)
            ?? throw new InvalidOperationException("Organization not found.");

        var requester = org.Users.FirstOrDefault(u => u.User?.AadId == userAadId);
        if (requester is null || requester.Role != OrganizationRole.Admin)
            throw new UnauthorizedAccessException("Only admins can view pending invitations.");

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
            });
    }
}
