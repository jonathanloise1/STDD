using MyApp.Domain.Entities;
using MyApp.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace MyApp.Infrastructure.Persistence;

public class MyAppDbContext(DbContextOptions<MyAppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationUser> OrganizationUsers => Set<OrganizationUser>();

    // Audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Tasks (example domain)
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder
            .ConfigureUser()
            .ConfigureOrganization()
            .ConfigureOrganizationUser()
            .ConfigureAuditLog()
            .ConfigureTaskItem();
    }
}
