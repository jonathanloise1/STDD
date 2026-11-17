namespace MyApp.Application.Exceptions;

/// <summary>
/// The user messages shared by the outcomes of every area: the same failure gets the same wording everywhere.
/// </summary>
public static class OutcomeMessages
{
    public const string ServiceUnavailable = "The service did not answer. Try again in a few minutes.";
    public const string RoleNotAllowed = "Your role does not allow this change.";
    public const string TaskNotFound = "This task no longer exists.";
    public const string MemberNotFound = "This member is no longer in the team.";
    public const string LastAdmin = "An organization needs at least one Admin.";
    public const string AlreadyMember = "User is already a member of this organization.";
    public const string AlreadyInvited = "A pending invitation for this email already exists.";
}
