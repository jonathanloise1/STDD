using MyApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API configuration for the <see cref="Organization"/> entity.
/// </summary>
public static class OrganizationConfiguration
{
    public static ModelBuilder ConfigureOrganization(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Organization>(e =>
        {
            e.HasIndex(o => o.VatNumber)
                .HasDatabaseName($"IX_{nameof(Organization)}_VatNumber");

            e.HasMany(o => o.Users)
                .WithOne(u => u.Organization)
                .HasForeignKey(u => u.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        return modelBuilder;
    }
}
