namespace MyApp.Domain.Constants;

/// <summary>
/// The entity types the audit trail offers as filter choices, with the friendly names users read.
/// The list is fixed: it does not depend on the changes recorded.
/// </summary>
public static class AuditEntityTypes
{
    public const string Task = "TaskItem";
    public const string TeamMember = "OrganizationUser";
    public const string Organization = "Organization";
    public const string User = "User";

    /// <summary>Stored entity type and friendly name, in the order of the filter choices.</summary>
    public static readonly IReadOnlyList<(string Value, string Name)> All =
    [
        (Task, "Task"),
        (TeamMember, "Team member"),
        (Organization, "Organization"),
        (User, "User"),
    ];

    /// <summary>The friendly name of a stored entity type; the stored value when it is not a known type.</summary>
    public static string NameOf(string entityType) =>
        All.FirstOrDefault(t => t.Value == entityType).Name ?? entityType;
}
