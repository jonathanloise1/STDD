namespace MyApp.Application.Interfaces;

/// <summary>
/// Provides the identity of the currently authenticated user.
/// Used by the audit interceptor to populate CreatedBy / ModifiedBy fields.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// The AadId (Entra objectidentifier) of the current user, or null for system/anonymous operations.
    /// </summary>
    Guid? UserId { get; }
}
