using MyApp.Domain.Entities;

namespace MyApp.Domain.Repositories;

/// <summary>
/// Repository interface for accessing and managing platform users.
/// </summary>
public interface IUsersRepository
{
    /// <summary>
    /// Retrieves a user by their internal system ID.
    /// </summary>
    Task<User?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a user by their email address.
    /// </summary>
    Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a user by email address within a specific organization.
    /// </summary>
    Task<User?> GetByEmailAndOrganizationAsync(
        string email,
        Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a user by their Azure AD (Entra External ID) identifier.
    /// </summary>
    Task<User?> GetByAadIdAsync(
        Guid? aadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all users belonging to a specific organization.
    /// </summary>
    Task<List<User>> GetByOrganizationIdAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new user to the system.
    /// </summary>
    Task AddAsync(
        User user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    Task UpdateAsync(
        User user,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first active organization membership for a user.
    /// </summary>
    Task<OrganizationUser?> GetFirstOrganizationUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the OrganizationUser ID from the authenticated user's AAD ID within a specific organization.
    /// Returns null if the user is not an active member.
    /// </summary>
    Task<Guid?> GetOrganizationUserIdByAadIdAsync(
        Guid organizationId,
        Guid aadId,
        CancellationToken cancellationToken = default);
}
