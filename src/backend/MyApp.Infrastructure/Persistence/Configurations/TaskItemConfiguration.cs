using MyApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyApp.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent API configuration for the <see cref="TaskItem"/> entity.
/// </summary>
public static class TaskItemConfiguration
{
    public static ModelBuilder ConfigureTaskItem(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskItem>(e =>
        {
            e.ToTable("TaskItems");

            e.HasIndex(t => new { t.OrganizationId, t.Status })
                .HasDatabaseName("IX_TaskItem_Org_Status");

            e.HasIndex(t => new { t.OrganizationId, t.AssignedToUserId })
                .HasDatabaseName("IX_TaskItem_Org_AssignedTo");

            e.HasIndex(t => t.DueDate)
                .HasDatabaseName("IX_TaskItem_DueDate");

            e.HasOne(t => t.Organization)
                .WithMany()
                .HasForeignKey(t => t.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(t => t.AssignedToUser)
                .WithMany()
                .HasForeignKey(t => t.AssignedToUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        return modelBuilder;
    }
}
