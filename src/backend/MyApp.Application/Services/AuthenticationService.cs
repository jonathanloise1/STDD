using MyApp.Application.DTOs.Users;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace MyApp.Application.Services;

/// <summary>
/// Authentication service that synchronizes users from Entra External ID
/// with the local database and returns their organization memberships.
/// </summary>
public class AuthenticationService(
    ILogger<AuthenticationService> logger,
    IUsersRepository usersRepository,
    IOrganizationsRepository organizationsRepository) : IAuthenticationService
{
    public async Task<AuthenticatedUserDto> SyncUserAsync(
        Guid userAadId,
        string firstName,
        string lastName,
        string email,
        Guid? organizationId,
        CancellationToken cancellationToken = default)
    {
        var user = await usersRepository.GetByAadIdAsync(userAadId, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                AadId = userAadId,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
            };

            await usersRepository.AddAsync(user, cancellationToken);
            logger.LogInformation("New user created: {UserId} ({Email})", user.Id, email);
        }
        else
        {
            // Update identity info from token if changed
            if (user.FirstName != firstName || user.LastName != lastName || user.Email != email)
            {
                user.FirstName = firstName;
                user.LastName = lastName;
                user.Email = email;
                await usersRepository.UpdateAsync(user, cancellationToken);
            }
        }

        // Auto-activate pending memberships matching this email
        await AutoActivatePendingMembershipsAsync(user, cancellationToken);

        // Load organizations
        var organizations = await organizationsRepository.GetByUserIdAsync(user.Id, cancellationToken);

        var orgDtos = organizations.Select(org =>
        {
            var orgUser = org.Users.FirstOrDefault(u => u.UserId == user.Id);
            return new AuthenticatedUserOrganizationDto
            {
                OrganizationId = org.Id,
                OrganizationName = org.Name,
                Role = orgUser?.Role.ToString() ?? "Viewer",
            };
        }).ToList();

        // US-AUTH-08: Include language preference in sync response
        // US-ONBOARD-01: Include onboarding state in sync response
        return new AuthenticatedUserDto
        {
            Id = user.Id,
            Email = user.Email,
            PreferredLanguage = user.PreferredLanguage,
            HasCompletedOnboarding = user.HasCompletedOnboarding,
            Organizations = orgDtos,
        };
    }

    private async Task AutoActivatePendingMembershipsAsync(User user, CancellationToken cancellationToken)
    {
        var activated = await organizationsRepository.ActivatePendingMembershipsByEmailAsync(
            user.Id, user.Email, cancellationToken);

        foreach (var membership in activated)
        {
            logger.LogInformation("Auto-activated pending membership for {Email} in org {OrgId}",
                user.Email, membership.OrganizationId);
        }
    }
}
