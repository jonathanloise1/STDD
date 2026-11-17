using MyApp.Domain.Entities;
using MyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Seed;

/// <summary>
/// Seeds the users, organizations and memberships of <see cref="SeedDataSet"/> for local development and
/// the E2E run, so the impersonation header works out of the box. Idempotent: what exists is kept.
/// The ids of the long-standing users (1 to 5) and organizations (0x100, 0x200) are unchanged.
/// </summary>
public static class SeedLocalDevelopment
{
    /// <summary>Well-known AadId of the local dev user (Admin). Must match VITE_DEV_MOCK_USER_ID.</summary>
    public static readonly Guid DevUserAadId = SeedDataSet.DevUser;

    /// <summary>Well-known id of "MyApp Dev Org".</summary>
    public static readonly Guid DevOrgId = SeedDataSet.DevOrg;

    /// <summary>Well-known AadId of the dev member (Editor).</summary>
    public static readonly Guid DevMemberAadId = SeedDataSet.DevMember;

    /// <summary>Well-known AadId of the dev viewer (read-only).</summary>
    public static readonly Guid DevViewerAadId = SeedDataSet.DevViewer;

    /// <summary>"Test UserC": no active membership, one pending invitation in "E2E Invited Org".</summary>
    public static readonly Guid E2EUserCAadId = SeedDataSet.UserC;

    /// <summary>"E2E Invited Org".</summary>
    public static readonly Guid E2EInvitedOrgId = SeedDataSet.InvitedOrg;

    /// <summary>"New User": no membership.</summary>
    public static readonly Guid E2ENewUserAadId = SeedDataSet.NewUser;

    public static async Task SeedAsync(MyAppDbContext context, ILogger? logger, DateTime now, CancellationToken ct = default)
    {
        var existingAad = (await context.Users.Select(u => u.AadId).ToListAsync(ct)).ToHashSet();
        foreach (var u in SeedDataSet.Users().Where(u => !existingAad.Contains(u.AadId)))
        {
            context.Users.Add(new User
            {
                Id = SeedDataSet.RowId(u.AadId),
                AadId = u.AadId,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                PhoneNumber = "",
                HasCompletedOnboarding = u.HasCompletedOnboarding,
                CreatedAt = now.AddDays(-60),
                UpdatedAt = now.AddDays(-60)
            });
        }

        var existingOrgs = (await context.Organizations.Select(o => o.Id).ToListAsync(ct)).ToHashSet();
        foreach (var org in SeedDataSet.Organizations(now).Where(o => !existingOrgs.Contains(o.Id)))
            context.Organizations.Add(org);

        await context.SaveChangesAsync(ct);

        var userIds = await UserIdsAsync(context, ct);
        var existingMemberships = await context.OrganizationUsers
            .Select(m => new { m.Id, m.OrganizationId, m.Email }).ToListAsync(ct);

        foreach (var m in SeedDataSet.Memberships())
        {
            if (existingMemberships.Any(e => e.Id == m.Id
                    || (e.OrganizationId == m.OrganizationId && string.Equals(e.Email, m.Email, StringComparison.OrdinalIgnoreCase))))
                continue;

            context.OrganizationUsers.Add(new OrganizationUser
            {
                Id = m.Id,
                OrganizationId = m.OrganizationId,
                UserId = m.UserAadId is { } aad ? userIds[aad] : null,
                Email = m.Email,
                FullName = m.FullName,
                Role = m.Role,
                Status = m.Status,
                CreatedAt = now.AddDays(-59),
                UpdatedAt = now.AddDays(-59)
            });
        }

        await context.SaveChangesAsync(ct);
        logger?.LogInformation("Local development seed data is in place.");
    }

    /// <summary>Maps the AadId of every user to the id of the user row.</summary>
    internal static async Task<Dictionary<Guid, Guid>> UserIdsAsync(MyAppDbContext context, CancellationToken ct) =>
        await context.Users.ToDictionaryAsync(u => u.AadId, u => u.Id, ct);
}
