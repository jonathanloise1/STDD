using MyApp.Domain.Constants;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MyApp.Infrastructure.Seed;

/// <summary>
/// Seeds example task data for manual testing and demo purposes.
/// Runs after SeedLocalDevelopment so that users and org already exist.
/// Idempotent: skips if demo tasks already exist.
/// </summary>
public static class SeedDemoData
{
    public static async Task SeedAsync(MyAppDbContext context, ILogger? logger, CancellationToken ct = default)
    {
        // Check if demo data already exists
        var alreadySeeded = await context.TaskItems
            .AnyAsync(t => t.Title == "Set up CI/CD pipeline", ct);

        if (alreadySeeded)
        {
            logger?.LogDebug("Demo data already seeded, skipping.");
            return;
        }

        // Look up the dev user and org
        var devUser = await context.Users.FirstOrDefaultAsync(u => u.AadId == SeedLocalDevelopment.DevUserAadId, ct);
        var memberUser = await context.Users.FirstOrDefaultAsync(u => u.AadId == SeedLocalDevelopment.DevMemberAadId, ct);
        var org = await context.Organizations.FirstOrDefaultAsync(o => o.Id == SeedLocalDevelopment.DevOrgId, ct);

        if (devUser is null || org is null)
        {
            logger?.LogWarning("Cannot seed demo data — dev user or org not found. Run SeedLocalDevelopment first.");
            return;
        }

        var userId = devUser.Id;
        var memberId = memberUser?.Id;
        var orgId = org.Id;
        var now = DateTime.UtcNow;

        // ═══════════════════════════════════════════════════════════════
        // TASKS — realistic project management tasks
        // ═══════════════════════════════════════════════════════════════

        var tasks = new List<TaskItem>
        {
            new()
            {
                OrganizationId = orgId,
                Title = "Set up CI/CD pipeline",
                Description = "Configure GitHub Actions for automated build, test, and deployment to Azure.",
                Status = TaskItemStatus.Done,
                AssignedToUserId = userId,
                DueDate = DateOnly.FromDateTime(now.AddDays(-10)),
                CreatedAt = now.AddDays(-30),
                UpdatedAt = now.AddDays(-10)
            },
            new()
            {
                OrganizationId = orgId,
                Title = "Design database schema",
                Description = "Create Entity-Relationship Diagram and define all entities, relationships, and indexes.",
                Status = TaskItemStatus.Done,
                AssignedToUserId = userId,
                DueDate = DateOnly.FromDateTime(now.AddDays(-15)),
                CreatedAt = now.AddDays(-25),
                UpdatedAt = now.AddDays(-14)
            },
            new()
            {
                OrganizationId = orgId,
                Title = "Implement user authentication",
                Description = "Integrate Microsoft Entra External ID for SSO. Set up token validation and user provisioning.",
                Status = TaskItemStatus.InProgress,
                AssignedToUserId = userId,
                DueDate = DateOnly.FromDateTime(now.AddDays(5)),
                CreatedAt = now.AddDays(-20),
                UpdatedAt = now.AddDays(-2)
            },
            new()
            {
                OrganizationId = orgId,
                Title = "Create task management UI",
                Description = "Build React pages for listing, creating, editing, and deleting tasks. Include filtering and pagination.",
                Status = TaskItemStatus.InProgress,
                AssignedToUserId = memberId,
                DueDate = DateOnly.FromDateTime(now.AddDays(10)),
                CreatedAt = now.AddDays(-15),
                UpdatedAt = now.AddDays(-1)
            },
            new()
            {
                OrganizationId = orgId,
                Title = "Write E2E tests for task CRUD",
                Description = "Cover happy path and error cases using Playwright. Test task creation, listing, update, and deletion.",
                Status = TaskItemStatus.Todo,
                AssignedToUserId = memberId,
                DueDate = DateOnly.FromDateTime(now.AddDays(15)),
                CreatedAt = now.AddDays(-10),
                UpdatedAt = now.AddDays(-10)
            },
            new()
            {
                OrganizationId = orgId,
                Title = "Add audit logging",
                Description = "Ensure all CRUD operations on tasks are captured in the audit log via EF Core interceptor.",
                Status = TaskItemStatus.Todo,
                AssignedToUserId = null,
                DueDate = DateOnly.FromDateTime(now.AddDays(20)),
                CreatedAt = now.AddDays(-8),
                UpdatedAt = now.AddDays(-8)
            },
            new()
            {
                OrganizationId = orgId,
                Title = "Set up monitoring dashboard",
                Description = "Configure Application Insights and create Azure Monitor workbook for key metrics.",
                Status = TaskItemStatus.Todo,
                AssignedToUserId = null,
                DueDate = null,
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-5)
            },
            new()
            {
                OrganizationId = orgId,
                Title = "Review security configuration",
                Description = "Audit CORS, rate limiting, authentication, and authorization settings before go-live.",
                Status = TaskItemStatus.Todo,
                AssignedToUserId = userId,
                DueDate = DateOnly.FromDateTime(now.AddDays(-3)),  // Overdue!
                CreatedAt = now.AddDays(-20),
                UpdatedAt = now.AddDays(-20)
            },
            new()
            {
                OrganizationId = orgId,
                Title = "Cancelled: Migrate to PostgreSQL",
                Description = "Originally planned to switch from SQL Server to PostgreSQL but decided to stay on SQL Server.",
                Status = TaskItemStatus.Cancelled,
                AssignedToUserId = userId,
                DueDate = null,
                CreatedAt = now.AddDays(-25),
                UpdatedAt = now.AddDays(-18)
            }
        };

        await context.TaskItems.AddRangeAsync(tasks, ct);
        await context.SaveChangesAsync(ct);

        logger?.LogInformation("Seeded {Count} demo tasks.", tasks.Count);
    }
}
