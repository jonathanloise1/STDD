using AutoMapper;
using MyApp.Domain.Entities;

namespace MyApp.Application.Automapper.Profiles;

/// <summary>
/// Base class for AutoMapper profiles with shared mapping helpers.
/// </summary>
public abstract class ProfileBase : Profile
{
    protected static string GetFullName(OrganizationUser? user)
    {
        return user?.User is not null
            ? $"{user.User.FirstName} {user.User.LastName}"
            : string.Empty;
    }
}
