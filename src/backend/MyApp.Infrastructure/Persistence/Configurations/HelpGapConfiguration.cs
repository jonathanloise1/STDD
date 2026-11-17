using MyApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MyApp.Infrastructure.Persistence.Configurations;

/// <summary>EF Core entity configuration for HelpGap.</summary>
// US-HELP-07
public static class HelpGapConfiguration
{
    public static ModelBuilder ConfigureHelpGap(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<HelpGap>(e =>
        {
            e.HasIndex(g => g.RecordedAt)
                .HasDatabaseName("IX_HelpGap_RecordedAt");
        });

        return modelBuilder;
    }
}
