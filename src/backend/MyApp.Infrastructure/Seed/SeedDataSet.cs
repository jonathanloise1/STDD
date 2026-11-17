using System.Text.Json;
using MyApp.Domain.Constants;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Seed;

/// <summary>A seeded user; the id of its row is <see cref="SeedDataSet.RowId"/>, never the AadId.</summary>
public sealed record SeedUser(Guid AadId, string FirstName, string LastName, string Email, bool HasCompletedOnboarding);

/// <summary>A seeded membership; <see cref="UserAadId"/> is null for a pending invitation.</summary>
public sealed record SeedMembership(
    Guid Id, Guid OrganizationId, Guid? UserAadId, string Email, string FullName,
    OrganizationRole Role, OrganizationUserStatus Status);

/// <summary>A seeded task; the assignee is given by the AadId of the user.</summary>
public sealed record SeedTask(
    Guid Id, Guid OrganizationId, string Title, string? Description, TaskItemStatus Status,
    Guid? AssignedToUserAadId, DateOnly? DueDate, DateTime CreatedAt, DateTime UpdatedAt);

/// <summary>A seeded audit entry; the author is given by the AadId of the user.</summary>
public sealed record SeedAuditEntry(
    Guid Id, Guid OrganizationId, string EntityType, string EntityId, AuditAction Action,
    Guid UserAadId, string UserDisplayName, string? OldValues, string? NewValues, string? ChangedProperties,
    DateTime Timestamp, string CorrelationId);

/// <summary>
/// The deterministic data set the test cases assume (the Seed Data of each area's testsuite.md).
/// Ids and names are fixed; every date is relative to the <c>now</c> given, which comes from the
/// configurable clock, so the same <c>now</c> always gives the same data. It is plain data: no database.
/// </summary>
public static class SeedDataSet
{
    public static Guid G(long n) => new($"00000000-0000-0000-0000-{n:x12}");

    /// <summary>
    /// The id of the user row of a seeded user. It differs from the AadId on purpose, as it does for a real user
    /// (the first sign-in gives the row a new id), so code that mixes the two up fails on the seed data too.
    /// </summary>
    public static Guid RowId(Guid aadId) => new($"10000000-0000-0000-0000-{aadId.ToString()[^12..]}");

    // ── Users (the AadId is fixed: impersonation and the E2E test users name it). 1 to 5 are the long-standing E2E users. ──
    public static readonly Guid DevUser = G(0x1);
    public static readonly Guid DevMember = G(0x2);
    public static readonly Guid UserC = G(0x3);
    public static readonly Guid NewUser = G(0x4);
    public static readonly Guid DevViewer = G(0x5);
    public static readonly Guid SpareMember = G(0x10);
    public static readonly Guid LeavingMember = G(0x11);
    public static readonly Guid FormerMember = G(0x12);
    public static readonly Guid InviteAdmin = G(0x20);
    public static readonly Guid StatsAdmin = G(0x30);
    public static readonly Guid StatsViewer = G(0x31);
    public static readonly Guid EmptyAdmin = G(0x40);
    public static readonly Guid SetupUser = G(0x50);

    // ── Organizations ──
    public static readonly Guid DevOrg = G(0x100);
    public static readonly Guid InvitedOrg = G(0x200);
    public static readonly Guid StatsOrg = G(0x300);
    public static readonly Guid EmptyOrg = G(0x400);

    public static IReadOnlyList<SeedUser> Users() =>
    [
        new(DevUser, "Dev", "User", "dev@myapp.local", true),
        new(DevMember, "Dev", "Member", "member@myapp.local", true),
        new(DevViewer, "Dev", "Viewer", "viewer@myapp.local", true),
        new(SpareMember, "Spare", "Member", "spare@myapp.local", true),
        new(LeavingMember, "Leaving", "Member", "leaving@myapp.local", true),
        new(FormerMember, "Former", "Member", "former@myapp.local", true),
        new(UserC, "Test", "UserC", "testc@myapp.test", true),
        new(NewUser, "New", "User", "new@myapp.test", false),
        new(InviteAdmin, "Invite", "Admin", "invite-admin@myapp.test", true),
        new(StatsAdmin, "Stats", "Admin", "stats-admin@myapp.test", true),
        new(StatsViewer, "Stats", "Viewer", "stats-viewer@myapp.test", true),
        new(EmptyAdmin, "Empty", "Admin", "empty-admin@myapp.test", true),
        new(SetupUser, "Setup", "User", "setup@myapp.test", false),
    ];

    public static IReadOnlyList<Organization> Organizations(DateTime now) =>
    [
        Org(DevOrg, "MyApp Dev Org", "MyApp Dev Org S.r.l.", "IT00000000000", "00000000000", "Via dello Sviluppo 1", "Milano", "MI", "20100", "dev@myapp.local", now),
        Org(InvitedOrg, "E2E Invited Org", "E2E Invited Org S.r.l.", "IT00000000200", "00000000200", "Via Invito 1", "Roma", "RM", "00100", "invited@myapp.test", now),
        Org(StatsOrg, "Stats Org", "Stats Org S.r.l.", "IT00000000300", "00000000300", "Via dei Numeri 3", "Torino", "TO", "10100", "stats@myapp.test", now),
        Org(EmptyOrg, "Empty Org", "Empty Org S.r.l.", "IT00000000400", "00000000400", "Via del Vuoto 4", "Bologna", "BO", "40100", "empty@myapp.test", now),
    ];

    private static Organization Org(Guid id, string name, string legal, string vat, string fiscal, string address,
        string city, string province, string zip, string email, DateTime now) => new()
    {
        Id = id, Name = name, LegalName = legal, VatNumber = vat, FiscalCode = fiscal,
        BillingAddress = address, BillingCity = city, BillingProvince = province, BillingZipCode = zip,
        BillingCountryCode = "IT", BillingEmail = email, LogoUrl = null, IsDeleted = false,
        CreatedAt = now.AddDays(-60), UpdatedAt = now.AddDays(-60)
    };

    public static IReadOnlyList<SeedMembership> Memberships() =>
    [
        Member(0x1001, DevOrg, DevUser, OrganizationRole.Admin),
        Member(0x1002, DevOrg, DevMember, OrganizationRole.Editor),
        Member(0x1003, DevOrg, DevViewer, OrganizationRole.Viewer),
        Member(0x1004, DevOrg, SpareMember, OrganizationRole.Viewer),
        Member(0x1005, DevOrg, LeavingMember, OrganizationRole.Viewer),
        Member(0x1006, DevOrg, FormerMember, OrganizationRole.Viewer),
        Member(0x1010, InvitedOrg, InviteAdmin, OrganizationRole.Admin),
        // Pending invitation for Test UserC: no user linked yet
        new(G(0x1011), InvitedOrg, null, "testc@myapp.test", "Test UserC", OrganizationRole.Editor, OrganizationUserStatus.Pending),
        Member(0x1020, StatsOrg, StatsAdmin, OrganizationRole.Admin),
        Member(0x1021, StatsOrg, StatsViewer, OrganizationRole.Viewer),
        Member(0x1030, EmptyOrg, EmptyAdmin, OrganizationRole.Admin),
    ];

    private static SeedMembership Member(long id, Guid org, Guid aad, OrganizationRole role)
    {
        var user = Users().First(u => u.AadId == aad);
        return new(G(id), org, aad, user.Email, $"{user.FirstName} {user.LastName}", role, OrganizationUserStatus.Active);
    }

    public static IReadOnlyList<SeedTask> Tasks(DateTime now)
    {
        var tasks = new List<SeedTask>
        {
            Dev(1, "Set up CI/CD pipeline", "Configure GitHub Actions for automated build, test, and deployment to Azure.", TaskItemStatus.Done, DevUser, -10, -30, -10, now),
            Dev(2, "Design database schema", "Create Entity-Relationship Diagram and define all entities, relationships, and indexes.", TaskItemStatus.Done, DevUser, -15, -25, -14, now),
            Dev(3, "Implement user authentication", "Integrate Microsoft Entra External ID for SSO. Set up token validation and user provisioning.", TaskItemStatus.InProgress, DevUser, 5, -20, -2, now),
            Dev(4, "Create task management UI", "Build React pages for listing, creating, editing, and deleting tasks. Include filtering and pagination.", TaskItemStatus.InProgress, DevMember, 10, -15, -1, now),
            Dev(5, "Write E2E tests for task CRUD", "Cover happy path and error cases using Playwright. Test task creation, listing, update, and deletion.", TaskItemStatus.Todo, DevMember, 15, -10, -10, now),
            Dev(6, "Add audit logging", "Ensure all CRUD operations on tasks are captured in the audit log via EF Core interceptor.", TaskItemStatus.Todo, null, 20, -8, -8, now),
            Dev(7, "Set up monitoring dashboard", "Configure Application Insights and create Azure Monitor workbook for key metrics.", TaskItemStatus.Todo, null, null, -5, -5, now),
            Dev(8, "Review security configuration", "Audit CORS, rate limiting, authentication, and authorization settings before go-live.", TaskItemStatus.Todo, DevUser, -3, -19, -19, now),
            Dev(9, "Cancelled: Migrate to PostgreSQL", "Originally planned to switch from SQL Server to PostgreSQL but decided to stay on SQL Server.", TaskItemStatus.Cancelled, DevUser, null, -26, -18, now),
        };

        // Stats Org: 5 Todo, 3 In Progress, 3 Done, 1 Cancelled; created in order on different days; 12 has no due date
        for (var i = 1; i <= 12; i++)
        {
            var status = i <= 5 ? TaskItemStatus.Todo : i <= 8 ? TaskItemStatus.InProgress : i <= 11 ? TaskItemStatus.Done : TaskItemStatus.Cancelled;
            var created = now.AddDays(i - 13);
            tasks.Add(new SeedTask(G(0x2100 + i), StatsOrg, $"Stats task {i:00}", $"Task {i} of the figures on the Dashboard.",
                status, null, i == 12 ? null : DateOnly.FromDateTime(now.AddDays(i)), created, created));
        }

        return tasks;
    }

    private static SeedTask Dev(int n, string title, string description, TaskItemStatus status, Guid? assignee,
        int? dueInDays, int createdDays, int updatedDays, DateTime now) =>
        new(G(0x2000 + n), DevOrg, title, description, status, assignee,
            dueInDays is null ? null : DateOnly.FromDateTime(now.AddDays(dueInDays.Value)),
            now.AddDays(createdDays), now.AddDays(updatedDays));

    /// <summary>
    /// The history of "MyApp Dev Org": 31 changes of types Task and Team member, all by "Dev User", between
    /// 24 days and 20 hours before <paramref name="now"/> (each has its own time).
    /// Nothing is audited while the seed runs (nobody is signed in), so the seed writes them itself.
    /// "Empty Org" has none.
    /// </summary>
    public static IReadOnlyList<SeedAuditEntry> AuditEntries(DateTime now)
    {
        var drafts = new List<(string Type, string Id, AuditAction Action, object? Old, object? New, string[]? Changed)>();
        var tasks = Tasks(now).Where(t => t.OrganizationId == DevOrg).ToList();
        var members = Memberships().Where(m => m.OrganizationId == DevOrg).ToList();

        foreach (var t in tasks)
            drafts.Add((AuditEntityTypes.Task, t.Id.ToString(), AuditAction.Create, null,
                new Dictionary<string, object?> { ["Title"] = t.Title, ["Description"] = t.Description, ["Status"] = (int)TaskItemStatus.Todo, ["DueDate"] = t.DueDate?.ToString("yyyy-MM-dd") }, null));

        foreach (var m in members)
            drafts.Add((AuditEntityTypes.TeamMember, m.Id.ToString(), AuditAction.Create, null,
                new Dictionary<string, object?> { ["Email"] = m.Email, ["FullName"] = m.FullName, ["Role"] = (int)m.Role, ["Status"] = (int)OrganizationUserStatus.Active }, null));

        foreach (var t in tasks.Where(t => t.Status != TaskItemStatus.Todo))
            drafts.Add((AuditEntityTypes.Task, t.Id.ToString(), AuditAction.Update,
                new Dictionary<string, object?> { ["Status"] = (int)TaskItemStatus.Todo },
                new Dictionary<string, object?> { ["Status"] = (int)t.Status }, ["Status"]));

        foreach (var t in tasks.Where(t => t.DueDate is not null).Take(3))
            drafts.Add((AuditEntityTypes.Task, t.Id.ToString(), AuditAction.Update,
                new Dictionary<string, object?> { ["DueDate"] = t.DueDate!.Value.AddDays(-7).ToString("yyyy-MM-dd") },
                new Dictionary<string, object?> { ["DueDate"] = t.DueDate!.Value.ToString("yyyy-MM-dd") }, ["DueDate"]));

        foreach (var t in tasks.Skip(4).Take(3))
            drafts.Add((AuditEntityTypes.Task, t.Id.ToString(), AuditAction.Update,
                new Dictionary<string, object?> { ["Title"] = t.Title + " (draft)" },
                new Dictionary<string, object?> { ["Title"] = t.Title }, ["Title"]));

        foreach (var t in tasks.Take(3))
            drafts.Add((AuditEntityTypes.Task, t.Id.ToString(), AuditAction.Update,
                new Dictionary<string, object?> { ["Description"] = "To be written." },
                new Dictionary<string, object?> { ["Description"] = t.Description }, ["Description"]));

        foreach (var m in members.Where(m => m.UserAadId is { } id && (id == DevMember)))
            drafts.Add((AuditEntityTypes.TeamMember, m.Id.ToString(), AuditAction.Update,
                new Dictionary<string, object?> { ["Role"] = (int)OrganizationRole.Viewer },
                new Dictionary<string, object?> { ["Role"] = (int)m.Role }, ["Role"]));

        drafts.Add((AuditEntityTypes.Task, G(0x2090).ToString(), AuditAction.Delete,
            new Dictionary<string, object?> { ["Title"] = "Obsolete task", ["Description"] = null, ["Status"] = (int)TaskItemStatus.Cancelled, ["DueDate"] = null },
            null, null));

        var json = new JsonSerializerOptions { WriteIndented = false };
        string? J(object? o) => o is null ? null : JsonSerializer.Serialize(o, json);

        return drafts.Select((d, k) => new SeedAuditEntry(
            G(0x3000 + k), DevOrg, d.Type, d.Id, d.Action, DevUser, "Dev User",
            J(d.Old), J(d.New), J(d.Changed),
            // oldest first, 18 hours apart, the newest 20 hours before now
            now.AddHours(-(20 + 18 * (drafts.Count - 1 - k))),
            G(0x5000 + k).ToString("N"))).ToList();
    }
}
