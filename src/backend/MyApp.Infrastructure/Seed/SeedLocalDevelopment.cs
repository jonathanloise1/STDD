using MyApp.Domain.Entities;
using MyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Seed;

/// <summary>
/// Seeds a development user and organization for local development.
/// This allows the ImpersonationMiddleware auth bypass to work out of the box
/// without needing to manually insert records via SQL.
///
/// The seeded AadId matches the VITE_DEV_MOCK_USER_ID in the frontend .env.local.
/// </summary>
public static class SeedLocalDevelopment
{
    /// <summary>
    /// Well-known AadId for the local dev user.
    /// Must match VITE_DEV_MOCK_USER_ID in frontend/.env.local.
    /// </summary>
    public static readonly Guid DevUserAadId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Well-known organization ID for the local dev organization.
    /// Used by E2E tests to verify organization data.
    /// </summary>
    public static readonly Guid DevOrgId = Guid.Parse("00000000-0000-0000-0000-000000000100");

    /// <summary>
    /// Well-known AadId for the local dev member user (non-admin).
    /// Used by E2E tests for member-role scenarios.
    /// </summary>
    public static readonly Guid DevMemberAadId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    /// <summary>
    /// Well-known AadId for the local dev viewer user (read-only).
    /// Used by E2E tests for viewer-role / authorization scenarios.
    /// </summary>
    public static readonly Guid DevViewerAadId = Guid.Parse("00000000-0000-0000-0000-000000000005");

    // ═══════════════════════════════════════════════════════════════════════
    // E2E TEST USERS (match Auth/TestUsers.cs in MyApp.E2E)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// User C — exists in DB but has NO active memberships.
    /// Has ONE pending invitation in "Invited Org".
    /// Used by AUTH-INVITE-001 to verify auto-activation on sync.
    /// </summary>
    public static readonly Guid E2EUserCAadId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    /// <summary>
    /// Organization for the invitation test.
    /// User C has a pending membership here.
    /// </summary>
    public static readonly Guid E2EInvitedOrgId = Guid.Parse("00000000-0000-0000-0000-000000000200");

    /// <summary>
    /// User "New" — exists in DB (needed for impersonation middleware)
    /// but has ZERO memberships and was just "provisioned".
    /// Used by AUTH-PROVISION-001 to verify sync returns empty orgs.
    /// </summary>
    public static readonly Guid E2ENewUserAadId = Guid.Parse("00000000-0000-0000-0000-000000000004");

    public static async Task SeedAsync(MyAppDbContext context, ILogger? logger, CancellationToken ct = default)
    {
        var userCreated = await EnsureDevUserAsync(context, logger, ct);
        var memberCreated = await EnsureDevMemberUserAsync(context, logger, ct);
        var viewerCreated = await EnsureDevViewerUserAsync(context, logger, ct);
        var orgCreated = await EnsureDevOrganizationAsync(context, logger, ct);

        // E2E test users
        var userCCreated = await EnsureE2EUserCAsync(context, logger, ct);
        var newUserCreated = await EnsureE2ENewUserAsync(context, logger, ct);
        var invitedOrgCreated = await EnsureE2EInvitedOrgAsync(context, logger, ct);

        if (userCreated || memberCreated || viewerCreated || orgCreated || userCCreated || newUserCreated || invitedOrgCreated)
        {
            await context.SaveChangesAsync(ct);
            logger?.LogInformation("Local development seed data saved successfully.");
        }

        // Ensure memberships exist (admin + member + pending invite)
        await EnsureDevMembershipAsync(context, logger, ct);
        await EnsureE2EPendingInvitationAsync(context, logger, ct);
    }

    private static async Task<bool> EnsureDevUserAsync(MyAppDbContext context, ILogger? logger, CancellationToken ct)
    {
        var exists = await context.Users.AnyAsync(u => u.AadId == DevUserAadId, ct);
        if (exists)
        {
            logger?.LogDebug("Dev user already exists (AadId: {AadId}), skipping.", DevUserAadId);
            return false;
        }

        var user = new User
        {
            AadId = DevUserAadId,
            FirstName = "Dev",
            LastName = "User",
            Email = "dev@MyApp.local",
            PhoneNumber = "",
            HasCompletedOnboarding = true, // US-ONBOARD-01: Existing users skip onboarding
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await context.Users.AddAsync(user, ct);
        logger?.LogInformation("Seeded dev user: {Email} (AadId: {AadId})", user.Email, user.AadId);
        return true;
    }

    private static async Task<bool> EnsureDevMemberUserAsync(MyAppDbContext context, ILogger? logger, CancellationToken ct)
    {
        var exists = await context.Users.AnyAsync(u => u.AadId == DevMemberAadId, ct);
        if (exists)
        {
            logger?.LogDebug("Dev member user already exists (AadId: {AadId}), skipping.", DevMemberAadId);
            return false;
        }

        var user = new User
        {
            AadId = DevMemberAadId,
            FirstName = "Dev",
            LastName = "Member",
            Email = "member@MyApp.local",
            PhoneNumber = "",
            HasCompletedOnboarding = true, // US-ONBOARD-01: Existing users skip onboarding
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await context.Users.AddAsync(user, ct);
        logger?.LogInformation("Seeded dev member user: {Email} (AadId: {AadId})", user.Email, user.AadId);
        return true;
    }

    private static async Task<bool> EnsureDevViewerUserAsync(MyAppDbContext context, ILogger? logger, CancellationToken ct)
    {
        var exists = await context.Users.AnyAsync(u => u.AadId == DevViewerAadId, ct);
        if (exists)
        {
            logger?.LogDebug("Dev viewer user already exists (AadId: {AadId}), skipping.", DevViewerAadId);
            return false;
        }

        var user = new User
        {
            AadId = DevViewerAadId,
            FirstName = "Dev",
            LastName = "Viewer",
            Email = "viewer@MyApp.local",
            PhoneNumber = "",
            HasCompletedOnboarding = true, // US-ONBOARD-01: Existing users skip onboarding
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await context.Users.AddAsync(user, ct);
        logger?.LogInformation("Seeded dev viewer user: {Email} (AadId: {AadId})", user.Email, user.AadId);
        return true;
    }

    private static async Task<bool> EnsureDevOrganizationAsync(MyAppDbContext context, ILogger? logger, CancellationToken ct)
    {
        var exists = await context.Organizations.AnyAsync(o => o.Id == DevOrgId, ct);
        if (exists)
        {
            logger?.LogDebug("Dev organization already exists (Id: {Id}), skipping.", DevOrgId);
            return false;
        }

        var org = new Organization
        {
            Id = DevOrgId,
            Name = "MyApp Dev Org",
            LegalName = "MyApp Dev Org S.r.l.",
            VatNumber = "IT00000000000",
            FiscalCode = "00000000000",
            BillingAddress = "Via dello Sviluppo 1",
            BillingCity = "Milano",
            BillingProvince = "MI",
            BillingZipCode = "20100",
            BillingCountryCode = "IT",
            BillingEmail = "dev@MyApp.local",
            LogoUrl = null,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Organizations.Add(org);
        logger?.LogInformation("Seeded dev organization: {Name}", org.Name);
        return true;
    }

    private static async Task<bool> EnsureE2EUserCAsync(MyAppDbContext context, ILogger? logger, CancellationToken ct)
    {
        var exists = await context.Users.AnyAsync(u => u.AadId == E2EUserCAadId, ct);
        if (exists)
        {
            logger?.LogDebug("E2E User C already exists (AadId: {AadId}), skipping.", E2EUserCAadId);
            return false;
        }

        var user = new User
        {
            AadId = E2EUserCAadId,
            FirstName = "Test",
            LastName = "UserC",
            Email = "testc@MyApp.test",
            PhoneNumber = "",
            HasCompletedOnboarding = true, // US-ONBOARD-01: Existing users skip onboarding
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await context.Users.AddAsync(user, ct);
        logger?.LogInformation("Seeded E2E User C: {Email} (AadId: {AadId})", user.Email, user.AadId);
        return true;
    }

    private static async Task<bool> EnsureE2ENewUserAsync(MyAppDbContext context, ILogger? logger, CancellationToken ct)
    {
        var exists = await context.Users.AnyAsync(u => u.AadId == E2ENewUserAadId, ct);
        if (exists)
        {
            logger?.LogDebug("E2E New User already exists (AadId: {AadId}), skipping.", E2ENewUserAadId);
            return false;
        }

        var user = new User
        {
            AadId = E2ENewUserAadId,
            FirstName = "New",
            LastName = "User",
            Email = "new@MyApp.test",
            PhoneNumber = "",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await context.Users.AddAsync(user, ct);
        logger?.LogInformation("Seeded E2E New User: {Email} (AadId: {AadId})", user.Email, user.AadId);
        return true;
    }

    private static async Task<bool> EnsureE2EInvitedOrgAsync(MyAppDbContext context, ILogger? logger, CancellationToken ct)
    {
        const string orgName = "E2E Invited Org";
        var exists = await context.Organizations.AnyAsync(o => o.Id == E2EInvitedOrgId && !o.IsDeleted, ct);
        if (exists)
        {
            logger?.LogDebug("E2E Invited Org already exists, skipping.");
            return false;
        }

        var org = new Organization
        {
            Id = E2EInvitedOrgId,
            Name = orgName,
            LegalName = "E2E Invited Org S.r.l.",
            VatNumber = "IT00000000200",
            FiscalCode = "00000000200",
            BillingAddress = "Via Invito 1",
            BillingCity = "Roma",
            BillingProvince = "RM",
            BillingZipCode = "00100",
            BillingCountryCode = "IT",
            BillingEmail = "invited@MyApp.test",
            LogoUrl = null,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Organizations.Add(org);
        logger?.LogInformation("Seeded E2E Invited Org: {Name}", org.Name);
        return true;
    }

    private static async Task EnsureE2EPendingInvitationAsync(MyAppDbContext context, ILogger? logger, CancellationToken ct)
    {
        var org = await context.Organizations.FirstOrDefaultAsync(o => o.Id == E2EInvitedOrgId && !o.IsDeleted, ct);
        if (org is null)
        {
            logger?.LogWarning("Cannot create pending invitation — E2E Invited Org not found.");
            return;
        }

        // Check if a pending invitation already exists for testc@MyApp.test in this org
        var alreadyExists = await context.OrganizationUsers
            .AnyAsync(ou => ou.Email == "testc@MyApp.test" && ou.OrganizationId == org.Id, ct);

        if (alreadyExists)
        {
            logger?.LogDebug("Pending invitation for testc@MyApp.test already exists, skipping.");
            return;
        }

        context.OrganizationUsers.Add(new OrganizationUser
        {
            Id = Guid.NewGuid(),
            UserId = null, // pending invite — no user linked yet
            OrganizationId = org.Id,
            Email = "testc@MyApp.test",
            FullName = "Test UserC",
            Role = OrganizationRole.Editor,
            Status = OrganizationUserStatus.Pending
        });

        await context.SaveChangesAsync(ct);
        logger?.LogInformation("Seeded pending invitation for testc@MyApp.test in E2E Invited Org.");
    }

    private static async Task EnsureDevMembershipAsync(MyAppDbContext context, ILogger? logger, CancellationToken ct)
    {
        var adminUser = await context.Users.FirstOrDefaultAsync(u => u.AadId == DevUserAadId, ct);
        var memberUser = await context.Users.FirstOrDefaultAsync(u => u.AadId == DevMemberAadId, ct);
        var viewerUser = await context.Users.FirstOrDefaultAsync(u => u.AadId == DevViewerAadId, ct);
        var org = await context.Organizations.FirstOrDefaultAsync(o => o.Id == DevOrgId && !o.IsDeleted, ct);

        if (org is null)
        {
            logger?.LogWarning("Cannot link dev users to org — org not found.");
            return;
        }

        // Link admin user
        if (adminUser is not null)
        {
            var alreadyLinked = await context.OrganizationUsers
                .AnyAsync(ou => ou.UserId == adminUser.Id && ou.OrganizationId == org.Id, ct);

            if (!alreadyLinked)
            {
                context.OrganizationUsers.Add(new OrganizationUser
                {
                    Id = Guid.NewGuid(),
                    UserId = adminUser.Id,
                    OrganizationId = org.Id,
                    Email = adminUser.Email,
                    FullName = $"{adminUser.FirstName} {adminUser.LastName}",
                    Role = OrganizationRole.Admin,
                    Status = OrganizationUserStatus.Active
                });
                logger?.LogInformation("Linked dev admin user to dev organization.");
            }
        }

        // Link member user
        if (memberUser is not null)
        {
            var alreadyLinked = await context.OrganizationUsers
                .AnyAsync(ou => ou.UserId == memberUser.Id && ou.OrganizationId == org.Id, ct);

            if (!alreadyLinked)
            {
                context.OrganizationUsers.Add(new OrganizationUser
                {
                    Id = Guid.NewGuid(),
                    UserId = memberUser.Id,
                    OrganizationId = org.Id,
                    Email = memberUser.Email,
                    FullName = $"{memberUser.FirstName} {memberUser.LastName}",
                    Role = OrganizationRole.Editor,
                    Status = OrganizationUserStatus.Active
                });
                logger?.LogInformation("Linked dev member user to dev organization.");
            }
        }

        // Link viewer user
        if (viewerUser is not null)
        {
            var alreadyLinked = await context.OrganizationUsers
                .AnyAsync(ou => ou.UserId == viewerUser.Id && ou.OrganizationId == org.Id, ct);

            if (!alreadyLinked)
            {
                context.OrganizationUsers.Add(new OrganizationUser
                {
                    Id = Guid.NewGuid(),
                    UserId = viewerUser.Id,
                    OrganizationId = org.Id,
                    Email = viewerUser.Email,
                    FullName = $"{viewerUser.FirstName} {viewerUser.LastName}",
                    Role = OrganizationRole.Viewer,
                    Status = OrganizationUserStatus.Active
                });
                logger?.LogInformation("Linked dev viewer user to dev organization.");
            }
        }

        await context.SaveChangesAsync(ct);
    }
}
