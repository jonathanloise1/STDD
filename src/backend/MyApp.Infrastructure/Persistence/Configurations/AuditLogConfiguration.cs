using MyApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core entity configuration for AuditLog.
/// <userstory ref="US-AUD-02" />
/// </summary>
public static class AuditLogConfiguration
{
    public static ModelBuilder ConfigureAuditLog(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.Property(a => a.Action)
                .HasConversion<string>()
                .HasMaxLength(50);

            e.HasIndex(a => new { a.EntityType, a.EntityId })
                .HasDatabaseName("IX_AuditLog_EntityType_EntityId");

            e.HasIndex(a => new { a.OrganizationId, a.Timestamp })
                .HasDatabaseName("IX_AuditLog_OrgId_Timestamp");

            e.HasIndex(a => a.UserId)
                .HasDatabaseName("IX_AuditLog_UserId");

            e.HasIndex(a => a.CorrelationId)
                .HasFilter("[CorrelationId] IS NOT NULL")
                .HasDatabaseName("IX_AuditLog_CorrelationId");

            e.HasOne(a => a.Organization)
                .WithMany()
                .HasForeignKey(a => a.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        return modelBuilder;
    }
}
