using MyApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API configuration for the <see cref="OrganizationUser"/> entity.
/// Stores enum properties (Role, Status) as strings in the database.
/// </summary>
public static class OrganizationUserConfiguration
{
    public static ModelBuilder ConfigureOrganizationUser(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationUser>(e =>
        {
            e.ToTable("OrganizationUsers");

            // Store enums as strings
            e.Property(ou => ou.Role)
                .HasConversion<string>()
                .HasMaxLength(50);

            e.Property(ou => ou.Status)
                .HasConversion<string>()
                .HasMaxLength(50);

            // Indexes
            e.HasIndex(ou => ou.OrganizationId)
                .HasDatabaseName($"IX_{nameof(OrganizationUser)}_OrganizationId");

            e.HasIndex(ou => ou.UserId)
                .HasDatabaseName($"IX_{nameof(OrganizationUser)}_UserId");

            e.HasIndex(ou => new { ou.Email, ou.OrganizationId })
                .HasDatabaseName($"IX_{nameof(OrganizationUser)}_Email_OrganizationId");

            // Relationships
            e.HasOne(ou => ou.User)
                .WithMany(u => u.UserOrganizations)
                .HasForeignKey(ou => ou.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        return modelBuilder;
    }
}
