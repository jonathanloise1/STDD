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
    /// Email address (used for Entra External ID login with OTP).
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// User role in the system.
    /// </summary>
    public required UserRole Role { get; init; }

    /// <summary>
    /// Organization ID the user belongs to.
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
/// Test users for E2E tests.
/// IDs match the seed data in SeedLocalDevelopment.cs.
/// </summary>
public static class TestUsers
{
    // ═══════════════════════════════════════════════════════════════════════
    // ORGANIZATION: MyApp Dev Org (matches SeedLocalDevelopment.DevOrgId)
    // ═══════════════════════════════════════════════════════════════════════

    public const string OrgDevId = "00000000-0000-0000-0000-000000000100";

    /// <summary>
    /// Admin user — full access to organization settings, user management, etc.
    /// Matches SeedLocalDevelopment.DevUserAadId.
    /// </summary>
    public static TestUser AdminUser => new()
    {
        Id = "00000000-0000-0000-0000-000000000001",
        Email = "dev@myapp.local",
        Role = UserRole.Admin,
        OrganizationId = OrgDevId
    };

    /// <summary>
    /// Member user — standard access within the organization.
    /// Matches SeedLocalDevelopment.DevMemberAadId.
    /// </summary>
    public static TestUser MemberUser => new()
    {
        Id = "00000000-0000-0000-0000-000000000002",
        Email = "member@myapp.local",
        Role = UserRole.Editor,
        OrganizationId = OrgDevId
    };

    /// <summary>
    /// Viewer user — read-only access within the organization.
    /// Matches SeedLocalDevelopment.DevViewerAadId.
    /// </summary>
    public static TestUser ViewerUser => new()
    {
        Id = "00000000-0000-0000-0000-000000000005",
        Email = "viewer@myapp.local",
        Role = UserRole.Viewer,
        OrganizationId = OrgDevId
    };

    // ═══════════════════════════════════════════════════════════════════════
    // E2E AUTH TEST USERS (no active org at seed time)
    // ═══════════════════════════════════════════════════════════════════════

    private const string OrgInvitedId = "00000000-0000-0000-0000-000000000200";

    /// <summary>
    /// User C — exists in DB with NO active memberships.
    /// Has a pending invitation in "E2E Invited Org" (auto-activates on sync).
    /// Used by AUTH-INVITE-001.
    /// Matches SeedLocalDevelopment.E2EUserCAadId.
    /// </summary>
    public static TestUser UserC => new()
    {
        Id = "00000000-0000-0000-0000-000000000003",
        Email = "testc@myapp.test",
        Role = UserRole.Editor,
        OrganizationId = OrgInvitedId
    };

    /// <summary>
    /// New User — exists in DB (required for impersonation middleware)
    /// but has zero memberships. Simulates a "first login" scenario.
    /// Used by AUTH-PROVISION-001.
    /// Matches SeedLocalDevelopment.E2ENewUserAadId.
    /// </summary>
    public static TestUser NewUser => new()
    {
        Id = "00000000-0000-0000-0000-000000000004",
        Email = "new@myapp.test",
        Role = UserRole.Viewer,
        OrganizationId = "" // no org
    };
}
