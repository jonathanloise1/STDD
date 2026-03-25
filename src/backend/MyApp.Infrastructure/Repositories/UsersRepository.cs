using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using MyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for accessing and managing platform users.
/// </summary>
public class UsersRepository(
    ILogger<UsersRepository> logger,
    MyAppDbContext context
) : IUsersRepository
{
    /// <inheritdoc />
    public async Task<User?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching user with ID: {UserId}", id);

        return await context.Users
            .Include(u => u.UserOrganizations).ThenInclude(uo => uo.Organization)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching user by email: {Email}", email);

        return await context.Users
            .Include(u => u.UserOrganizations).ThenInclude(uo => uo.Organization)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> GetByEmailAndOrganizationAsync(
        string email,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching user by email: {Email} and organization: {OrganizationId}", email, organizationId);

        return await context.Users
            .Include(u => u.UserOrganizations).ThenInclude(uo => uo.Organization)
            .FirstOrDefaultAsync(
                u => u.Email.ToLower() == email.ToLower() &&
                     u.UserOrganizations.Any(uo => uo.OrganizationId == organizationId),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> GetByAadIdAsync(
        Guid? aadId,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching user by AAD ID: {AadId}", aadId);

        return await context.Users
            .Include(u => u.UserOrganizations).ThenInclude(uo => uo.Organization)
            .FirstOrDefaultAsync(u => u.AadId == aadId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<User>> GetByOrganizationIdAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching users for organization: {OrganizationId}", organizationId);

        return await context.Users
            .Include(u => u.UserOrganizations)
            .Where(u => u.UserOrganizations.Any(uo => uo.OrganizationId == organizationId))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Adding new user: {Email}", user.Email);

        await context.Users.AddAsync(user, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Updating user: {UserId}", user.Id);

        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OrganizationUser?> GetFirstOrganizationUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching first organization for user: {UserId}", userId);

        var user = await context.Users
            .Include(u => u.UserOrganizations)
                .ThenInclude(uo => uo.Organization)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user?.UserOrganizations
            .FirstOrDefault(ou => ou.Status == OrganizationUserStatus.Active);
    }

    /// <inheritdoc />
    public async Task<Guid?> GetOrganizationUserIdByAadIdAsync(
        Guid organizationId,
        Guid aadId,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Resolving OrganizationUser for OrgId: {OrganizationId}, AadId: {AadId}",
            organizationId, aadId);

        return await context.OrganizationUsers
            .Where(ou => ou.OrganizationId == organizationId
                && ou.User != null
                && ou.User.AadId == aadId
                && ou.Status == OrganizationUserStatus.Active)
            .Select(ou => (Guid?)ou.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
