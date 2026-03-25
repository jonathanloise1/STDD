using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyApp.Application.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Persistence;

/// <summary>
/// EF Core interceptor that automatically populates audit fields
/// (CreatedAt, CreatedBy, UpdatedAt, ModifiedBy) on save.
/// Works with any entity implementing <see cref="IAuditableEntity"/>.
/// </summary>
public class AuditableEntityInterceptor(ICurrentUserService currentUserService) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context == null) return;

        var now = DateTime.UtcNow;
        var userId = currentUserService.UserId;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = userId;
                entry.Entity.UpdatedAt = now;
                entry.Entity.ModifiedBy = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                // Prevent overwriting creation fields on updates
                entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;

                entry.Entity.UpdatedAt = now;
                entry.Entity.ModifiedBy = userId;
            }
        }
    }
}
