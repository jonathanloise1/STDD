using Microsoft.Extensions.Logging;

namespace MyApp.Application.Logging;

/// <summary>Log definitions of the Organizations area (operations.md 3.1).</summary>
public static partial class OrganizationsLog
{
    public static class Keys
    {
        public const string OrgViewUnavailable = "org.view.unavailable";
        public const string OrgUpdateUnavailable = "org.update.unavailable";
        public const string OrgLogoUnavailable = "org.logo.unavailable";
        public const string OrgTeamUnavailable = "org.team.unavailable";
        public const string OrgRoleNotFound = "org.role.not_found";
        public const string OrgRemoveNotFound = "org.remove.not_found";
        public const string OrgInviteAlreadyMember = "org.invite.already_member";
        public const string OrgInviteAlreadyInvited = "org.invite.already_invited";
        public const string OrgRoleLastAdmin = "org.role.last_admin";
    }

    // US-ORG-01
    [LoggerMessage(EventId = 1, EventName = Keys.OrgViewUnavailable, Level = LogLevel.Error,
        Message = "Organization not loaded: the service did not answer ({ExceptionType} at {TopFrame})")]
    public static partial void OrgViewUnavailable(this ILogger logger, Exception exception, string exceptionType, string topFrame);

    // US-ORG-02
    [LoggerMessage(EventId = 2, EventName = Keys.OrgUpdateUnavailable, Level = LogLevel.Error,
        Message = "Organization not saved: the service did not answer ({ExceptionType} at {TopFrame})")]
    public static partial void OrgUpdateUnavailable(this ILogger logger, Exception exception, string exceptionType, string topFrame);

    // US-ORG-03
    [LoggerMessage(EventId = 3, EventName = Keys.OrgLogoUnavailable, Level = LogLevel.Error,
        Message = "Logo not uploaded: the service did not answer ({ExceptionType} at {TopFrame})")]
    public static partial void OrgLogoUnavailable(this ILogger logger, Exception exception, string exceptionType, string topFrame);

    // US-ORG-04
    [LoggerMessage(EventId = 4, EventName = Keys.OrgTeamUnavailable, Level = LogLevel.Error,
        Message = "Team not loaded: the service did not answer ({ExceptionType} at {TopFrame})")]
    public static partial void OrgTeamUnavailable(this ILogger logger, Exception exception, string exceptionType, string topFrame);

    // US-ORG-06
    [LoggerMessage(EventId = 5, EventName = Keys.OrgRoleNotFound, Level = LogLevel.Warning,
        Message = "Role not changed: membership {MembershipId} is no longer in organization {OrganizationId}")]
    public static partial void OrgRoleNotFound(this ILogger logger, Guid membershipId, Guid organizationId);

    // US-ORG-07
    [LoggerMessage(EventId = 6, EventName = Keys.OrgRemoveNotFound, Level = LogLevel.Warning,
        Message = "Member not removed: membership {MembershipId} is no longer in organization {OrganizationId}")]
    public static partial void OrgRemoveNotFound(this ILogger logger, Guid membershipId, Guid organizationId);

    // US-ORG-05
    [LoggerMessage(EventId = 7, EventName = Keys.OrgInviteAlreadyMember, Level = LogLevel.Information,
        Message = "Invitation not sent: the person is already a member of organization {OrganizationId}")]
    public static partial void OrgInviteAlreadyMember(this ILogger logger, Guid organizationId);

    // US-ORG-05
    [LoggerMessage(EventId = 8, EventName = Keys.OrgInviteAlreadyInvited, Level = LogLevel.Information,
        Message = "Invitation not sent: a pending invitation already exists in organization {OrganizationId}")]
    public static partial void OrgInviteAlreadyInvited(this ILogger logger, Guid organizationId);

    // US-ORG-06
    [LoggerMessage(EventId = 9, EventName = Keys.OrgRoleLastAdmin, Level = LogLevel.Information,
        Message = "Role not changed: membership {MembershipId} is the last Admin of organization {OrganizationId}")]
    public static partial void OrgRoleLastAdmin(this ILogger logger, Guid membershipId, Guid organizationId);
}
