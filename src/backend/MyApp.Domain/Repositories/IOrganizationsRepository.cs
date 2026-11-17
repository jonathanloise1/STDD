using MyApp.Domain.Entities;

namespace MyApp.Domain.Repositories;

/// <summary>
/// Repository interface for accessing and managing organizations.
/// </summary>
public interface IOrganizationsRepository
{
    /// <summary>
    /// Retrieves an organization by its unique identifier.
    /// </summary>
    /// <param name="id">The unique ID of the organization.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The organization if found; otherwise, null.</returns>
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new organization to the data store.
    /// </summary>
    /// <param name="organization">The organization entity to add.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task AddAsync(Organization organization, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing organization in the data store.
    /// </summary>
    /// <param name="organization">The updated organization entity.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task UpdateAsync(Organization organization, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all organizations where a user with the specified AAD ID is a member.
    /// </summary>
    Task<List<Organization>> GetByUserAadIdAsync(
        Guid userAadId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates all pending OrganizationUser records matching the given email.
    /// Sets UserId and Status = Active, then persists changes.
    /// </summary>
    /// <param name="userId">The User.Id to link to the pending memberships.</param>
    /// <param name="email">The email to match (case-insensitive).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The list of activated memberships.</returns>
    Task<List<OrganizationUser>> ActivatePendingMembershipsByEmailAsync(
        Guid userId,
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all organizations where a user with the specified ID is an active member.
    /// </summary>
    Task<List<Organization>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new OrganizationUser directly to the data store.
    /// Used for invite flows where the org is already tracked and calling Update()
    /// on the parent would cause concurrency issues with newly-added child entities.
    /// </summary>
    Task AddOrganizationUserAsync(OrganizationUser orgUser, CancellationToken cancellationToken = default);
}
