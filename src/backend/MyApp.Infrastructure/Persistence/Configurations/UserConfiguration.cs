using MyApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API configuration for the <see cref="User"/> entity.
/// </summary>
public static class UserConfiguration
{
    public static ModelBuilder ConfigureUser(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");

            e.HasIndex(u => u.AadId)
                .HasDatabaseName("IX_User_AadId")
                .IsUnique();

            e.HasMany(u => u.UserOrganizations)
                .WithOne(o => o.User)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        return modelBuilder;
    }
}
