using MyApp.Application.DTOs.Users;

namespace MyApp.Application.Interfaces;

/// <summary>
/// Contract for the authentication service that synchronizes users from Entra External ID
/// with the local database and returns their organization memberships.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Synchronizes a user based on their Entra External ID identity.
    /// Creates the user if they don't exist, and auto-accepts pending organization invitations.
    /// </summary>
    Task<AuthenticatedUserDto> SyncUserAsync(
        Guid userAadId,
        string firstName,
        string lastName,
        string email,
        Guid? organizationId,
        CancellationToken cancellationToken = default);
}
