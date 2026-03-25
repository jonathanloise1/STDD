using MyApp.Application.DTOs.Organizations;
using MyApp.Domain.Entities;

namespace MyApp.Application.Automapper.Profiles;

/// <summary>
/// AutoMapper profile for organization-related mappings.
/// </summary>
public class OrganizationProfile : ProfileBase
{
    public OrganizationProfile()
    {
        // Organization -> OrganizationDto
        CreateMap<Organization, OrganizationDto>();

        // OrganizationUser -> OrganizationUserDto
        CreateMap<OrganizationUser, OrganizationUserDto>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.User == null ? Guid.Empty : src.User.Id))
            .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.User != null ? src.User.FirstName : (src.FullName ?? "")))
            .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.User != null ? src.User.LastName : ""))
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.User != null ? src.User.Email : src.Email));

        // CreateOrganizationRequest -> Organization
        CreateMap<CreateOrganizationRequest, Organization>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Users, opt => opt.Ignore());

        // UpdateOrganizationRequest -> Organization
        CreateMap<UpdateOrganizationRequest, Organization>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Users, opt => opt.Ignore());

        // InviteCollaboratorRequest -> OrganizationUser
        CreateMap<InviteCollaboratorRequest, OrganizationUser>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.OrganizationId, opt => opt.Ignore())
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.MapFrom(_ => OrganizationUserStatus.Pending))
            .ForMember(dest => dest.Role, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore())
            .ForMember(dest => dest.Organization, opt => opt.Ignore());

        // UpdateOrganizationUserRequest -> OrganizationUser
        CreateMap<UpdateOrganizationUserRequest, OrganizationUser>()
            .ForMember(dest => dest.Role, opt => opt.Ignore())
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.OrganizationId, opt => opt.Ignore())
            .ForMember(dest => dest.UserId, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.User, opt => opt.Ignore())
            .ForMember(dest => dest.Organization, opt => opt.Ignore());
    }
}
