namespace MyApp.E2E.Auth;

/// <summary>
/// Test user record containing all authentication and identity information.
/// </summary>
public record TestUser
{
    /// <summary>
    /// User ID (matches seed data GUIDs in the database).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Name as the UI shows it (for example the Name column of the Team page).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Email address (used for Entra External ID login with OTP).
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// Role in the organization the user belongs to at seed time.
    /// </summary>
    public required UserRole Role { get; init; }

    /// <summary>
    /// Organization ID the user belongs to at seed time; empty when the user belongs to none.
    /// </summary>
    public required string OrganizationId { get; init; }
}

/// <summary>
/// Roles available in the system.
/// Each user belongs to an organization with one of these roles.
/// </summary>
public enum UserRole
{
    Admin,
    Editor,
    Viewer
}

/// <summary>
/// The users of the seed data (development.md 10.3; the Seed Data of each area's testsuite.md).
/// Ids, names and emails are those of <c>SeedDataSet</c> in the backend: the seed creates them when the
/// Development backend starts, so a test only names them here. A test that changes a user (role, language,
/// membership) restores the state through the UI.
/// </summary>
public static class TestUsers
{
    private const string IdPrefix = "00000000-0000-0000-0000-";

    // Organizations of the seed data
    public const string OrgDevId = IdPrefix + "000000000100";
    public const string OrgInvitedId = IdPrefix + "000000000200";
    public const string OrgStatsId = IdPrefix + "000000000300";
    public const string OrgEmptyId = IdPrefix + "000000000400";

    // Organization names as the UI shows them
    public const string OrgDevName = "MyApp Dev Org";
    public const string OrgInvitedName = "E2E Invited Org";
    public const string OrgStatsName = "Stats Org";
    public const string OrgEmptyName = "Empty Org";

    private static TestUser User(string suffix, string name, string email, UserRole role, string organizationId) => new()
    {
        Id = IdPrefix + suffix,
        Name = name,
        Email = email,
        Role = role,
        OrganizationId = organizationId
    };

    // ----- "MyApp Dev Org": one user per role -----

    /// <summary>Dev User: the only Admin of "MyApp Dev Org".</summary>
    public static TestUser AdminUser => User("000000000001", "Dev User", "dev@myapp.local", UserRole.Admin, OrgDevId);

    /// <summary>Dev Member: Editor of "MyApp Dev Org".</summary>
    public static TestUser MemberUser => User("000000000002", "Dev Member", "member@myapp.local", UserRole.Editor, OrgDevId);

    /// <summary>Dev Viewer: Viewer of "MyApp Dev Org".</summary>
    public static TestUser ViewerUser => User("000000000005", "Dev Viewer", "viewer@myapp.local", UserRole.Viewer, OrgDevId);

    /// <summary>Spare Member: Viewer of "MyApp Dev Org", free to be changed by team tests.</summary>
    public static TestUser SpareMember => User("000000000010", "Spare Member", "spare@myapp.local", UserRole.Viewer, OrgDevId);

    /// <summary>Leaving Member: Viewer of "MyApp Dev Org", free to be removed by team tests.</summary>
    public static TestUser LeavingMember => User("000000000011", "Leaving Member", "leaving@myapp.local", UserRole.Viewer, OrgDevId);

    /// <summary>Former Member: Viewer of "MyApp Dev Org" for the "member is already gone" cases.</summary>
    public static TestUser FormerMember => User("000000000012", "Former Member", "former@myapp.local", UserRole.Viewer, OrgDevId);

    // ----- Other organizations -----

    /// <summary>Invite Admin: Admin of "E2E Invited Org".</summary>
    public static TestUser InviteAdmin => User("000000000020", "Invite Admin", "invite-admin@myapp.test", UserRole.Admin, OrgInvitedId);

    /// <summary>Stats Admin: Admin of "Stats Org", which holds the tasks the dashboard counts.</summary>
    public static TestUser StatsAdmin => User("000000000030", "Stats Admin", "stats-admin@myapp.test", UserRole.Admin, OrgStatsId);

    /// <summary>Stats Viewer: Viewer of "Stats Org".</summary>
    public static TestUser StatsViewer => User("000000000031", "Stats Viewer", "stats-viewer@myapp.test", UserRole.Viewer, OrgStatsId);

    /// <summary>Empty Admin: the only member of "Empty Org", which has no tasks.</summary>
    public static TestUser EmptyAdmin => User("000000000040", "Empty Admin", "empty-admin@myapp.test", UserRole.Admin, OrgEmptyId);

    // ----- Users with no active organization -----

    /// <summary>Test UserC: belongs to no organization yet; holds a pending invitation to "E2E Invited Org" as Editor.</summary>
    public static TestUser UserC => User("000000000003", "Test UserC", "testc@myapp.test", UserRole.Editor, OrgInvitedId);

    /// <summary>New User: belongs to no organization and has not completed onboarding.</summary>
    public static TestUser NewUser => User("000000000004", "New User", "new@myapp.test", UserRole.Viewer, "");

    /// <summary>Setup User: belongs to no organization and has not completed onboarding; for the setup wizard.</summary>
    public static TestUser SetupUser => User("000000000050", "Setup User", "setup@myapp.test", UserRole.Viewer, "");
}
