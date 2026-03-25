using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using MyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for organization data access and manipulation.
/// </summary>
/// <userstory ref="US-ORG-01, US-ORG-02, US-ORG-03, US-ORG-04, US-ORG-05, US-ORG-06, US-TEAM-01, US-TEAM-05, US-TEAM-06, US-TEAM-07, US-TEAM-11" />
public class OrganizationsRepository(
    ILogger<OrganizationsRepository> logger,
    MyAppDbContext context) : IOrganizationsRepository
{
    /// <inheritdoc />
    public async Task<Organization?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching organization with ID: {OrganizationId}", id);

        var organization = await context.Organizations
            .Include(o => o.Users).ThenInclude(ou => ou.User)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (organization is null)
        {
            logger.LogWarning("Organization not found with ID: {OrganizationId}", id);
        }
        else
        {
            logger.LogInformation("Organization retrieved with ID: {OrganizationId}", id);
        }

        return organization;
    }

    /// <inheritdoc />
    public async Task AddAsync(
        Organization organization,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Adding new organization with name: {OrganizationName}", organization.Name);

        await context.Organizations.AddAsync(organization, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Organization successfully added with generated ID: {OrganizationId}", organization.Id);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        Organization organization,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Updating organization with ID: {OrganizationId}", organization.Id);

        // If the entity is already tracked (loaded via GetByIdAsync), calling
        // context.Update() would re-mark all related entities — including newly
        // added child entities that have a pre-set Guid key — as Modified instead
        // of Added, which causes "affected 0 row(s)" concurrency errors.
        // Only call Update() for detached entities; otherwise rely on change tracking.
        var entry = context.Entry(organization);
        if (entry.State == EntityState.Detached)
        {
            context.Organizations.Update(organization);
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Organization successfully updated with ID: {OrganizationId}", organization.Id);
    }

    /// <inheritdoc />
    public async Task AddOrganizationUserAsync(
        OrganizationUser orgUser,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Adding OrganizationUser {Email} to organization {OrgId}",
            orgUser.Email, orgUser.OrganizationId);

        await context.OrganizationUsers.AddAsync(orgUser, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogDebug("OrganizationUser added with ID: {OrgUserId}", orgUser.Id);
    }

    /// <inheritdoc />
    public async Task<List<Organization>> GetByUserAadIdAsync(
        Guid userAadId,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving organizations for user with AAD ID: {UserAadId}", userAadId);

        var organizations = await context.Organizations
            .Include(o => o.Users)
                .ThenInclude(u => u.User)
            .Where(o => o.Users.Any(u => u.User != null && u.User.AadId == userAadId))
            .Where(o => !o.IsDeleted)
            .ToListAsync(cancellationToken);

        logger.LogInformation("Found {Count} organizations for user with AAD ID: {UserAadId}", 
            organizations.Count, userAadId);

        return organizations;
    }

    /// <inheritdoc />
    public async Task<List<OrganizationUser>> ActivatePendingMembershipsByEmailAsync(
        Guid userId, string email, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Looking for pending memberships for email: {Email}", email);

        var pending = await context.OrganizationUsers
            .Where(ou => ou.Email.ToLower() == email.ToLower()
                         && ou.Status == OrganizationUserStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var membership in pending)
        {
            membership.UserId = userId;
            membership.Status = OrganizationUserStatus.Active;
        }

        if (pending.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Activated {Count} pending memberships for {Email}", pending.Count, email);
        }

        return pending;
    }

    /// <inheritdoc />
    public async Task<List<Organization>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Retrieving organizations for user: {UserId}", userId);

        var organizations = await context.Organizations
            .Include(o => o.Users)
                .ThenInclude(u => u.User)
            .Where(o => o.Users.Any(u => 
                u.UserId == userId && 
                u.Status == OrganizationUserStatus.Active))
            .Where(o => !o.IsDeleted)
            .ToListAsync(cancellationToken);

        logger.LogInformation("Found {Count} organizations for user: {UserId}", 
            organizations.Count, userId);

        return organizations;
    }
}
