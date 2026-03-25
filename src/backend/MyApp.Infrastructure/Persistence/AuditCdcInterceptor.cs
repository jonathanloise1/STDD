using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyApp.Application.Interfaces;
using MyApp.Domain.Constants;
using MyApp.Domain.Entities;

namespace MyApp.Infrastructure.Persistence;

/// <summary>
/// EF Core SaveChanges interceptor that captures entity mutations and writes
/// append-only <see cref="AuditLog"/> records.
/// <userstory ref="US-AUD-03, US-AUD-04" />
/// </summary>
/// <remarks>
/// Excluded entity types (no audit trail):
/// AuditLog (prevent recursion), CalculationRun, AllocationResult, NodeResult,
/// AssetDepreciation, ImportSessionEntry, CostEntryDestination.
/// </remarks>
public class AuditCdcInterceptor(ICurrentUserService currentUserService) : SaveChangesInterceptor
{
    private static readonly HashSet<string> ExcludedTypes =
    [
        nameof(AuditLog)
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CaptureAndWriteAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureAndWriteAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // ── Capture changes and add AuditLog entries before save ───────────

    private void CaptureAndWriteAuditLogs(DbContext? context)
    {
        if (context is null) return;

        var entries = CaptureEntries(context);
        if (entries is null || entries.Count == 0) return;

        var userId = ResolveUserId(context);
        var userDisplayName = ResolveUserDisplayName(context);

        // Skip audit logs when there is no authenticated user (e.g. during seed / migrations)
        if (userId == Guid.Empty) return;

        foreach (var ae in entries)
        {
            if (ae.OrganizationId == Guid.Empty) continue;

            var log = new AuditLog
            {
                Id = Guid.NewGuid(),
                OrganizationId = ae.OrganizationId,
                EntityType = ae.EntityType,
                EntityId = ae.EntityId,
                Action = ae.State switch
                {
                    EntityState.Added => AuditAction.Create,
                    EntityState.Modified => AuditAction.Update,
                    EntityState.Deleted => AuditAction.Delete,
                    _ => AuditAction.Update
                },
                UserId = userId,
                UserDisplayName = userDisplayName,
                OldValues = ae.OldValues is not null
                    ? JsonSerializer.Serialize(ae.OldValues, JsonOptions) : null,
                NewValues = ae.NewValues is not null
                    ? JsonSerializer.Serialize(ae.NewValues, JsonOptions) : null,
                ChangedProperties = ae.ChangedProperties is not null
                    ? JsonSerializer.Serialize(ae.ChangedProperties, JsonOptions) : null,
                Timestamp = DateTime.UtcNow,
                CorrelationId = ae.CorrelationId
            };

            // Add to context — will be persisted in the same SaveChanges call
            context.Set<AuditLog>().Add(log);
        }
    }

    // ── Capture entity change entries ──────────────────────────────────

    private List<AuditEntry>? CaptureEntries(DbContext context)
    {

        var entries = new List<AuditEntry>();
        var correlationId = Guid.NewGuid().ToString("N");

        foreach (var entry in context.ChangeTracker.Entries())
        {
            var typeName = entry.Entity.GetType().Name;
            if (ExcludedTypes.Contains(typeName)) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var auditEntry = new AuditEntry
            {
                EntityType = typeName,
                State = entry.State,
                CorrelationId = correlationId
            };

            // Capture entity ID (primary key)
            var pkProp = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
            auditEntry.EntityId = pkProp?.CurrentValue?.ToString() ?? string.Empty;

            // Capture values based on state
            switch (entry.State)
            {
                case EntityState.Added:
                    auditEntry.NewValues = GetPropertyValues(entry, e => e.CurrentValue);
                    break;

                case EntityState.Deleted:
                    auditEntry.OldValues = GetPropertyValues(entry, e => e.OriginalValue);
                    break;

                case EntityState.Modified:
                    auditEntry.OldValues = GetModifiedOriginalValues(entry);
                    auditEntry.NewValues = GetModifiedCurrentValues(entry);
                    auditEntry.ChangedProperties = entry.Properties
                        .Where(p => p.IsModified)
                        .Select(p => p.Metadata.Name)
                        .ToList();
                    break;
            }

            // Resolve OrganizationId
            auditEntry.OrganizationId = ResolveOrganizationId(entry, context);

            entries.Add(auditEntry);
        }

        return entries.Count > 0 ? entries : null;
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static Dictionary<string, object?> GetPropertyValues(
        EntityEntry entry, Func<PropertyEntry, object?> valueSelector)
    {
        return entry.Properties
            .Where(p => !p.Metadata.IsPrimaryKey())
            .ToDictionary(p => p.Metadata.Name, valueSelector);
    }

    private static Dictionary<string, object?> GetModifiedOriginalValues(EntityEntry entry)
    {
        return entry.Properties
            .Where(p => p.IsModified)
            .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
    }

    private static Dictionary<string, object?> GetModifiedCurrentValues(EntityEntry entry)
    {
        return entry.Properties
            .Where(p => p.IsModified)
            .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
    }

    /// <summary>
    /// Resolves OrganizationId from the entity — direct property first.
    /// </summary>
    private Guid ResolveOrganizationId(EntityEntry entry, DbContext context)
    {
        // Direct OrganizationId property
        var orgProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "OrganizationId");
        if (orgProp?.CurrentValue is Guid orgId && orgId != Guid.Empty)
            return orgId;

        // For Organization entity itself
        if (entry.Entity is Organization org)
            return org.Id;

        return Guid.Empty;
    }

    /// <summary>Resolves User.Id from the current AAD objectidentifier.</summary>
    private Guid ResolveUserId(DbContext context)
    {
        var aadId = currentUserService.UserId;
        if (aadId is null || aadId == Guid.Empty) return Guid.Empty;

        return context.Set<User>()
            .Where(u => u.AadId == aadId.Value)
            .Select(u => u.Id)
            .FirstOrDefault();
    }

    /// <summary>Resolves display name from the User table.</summary>
    private string? ResolveUserDisplayName(DbContext context)
    {
        var aadId = currentUserService.UserId;
        if (aadId is null || aadId == Guid.Empty) return null;

        return context.Set<User>()
            .Where(u => u.AadId == aadId.Value)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstOrDefault();
    }

    /// <summary>Internal record to hold pre-save state.</summary>
    private sealed class AuditEntry
    {
        public string EntityType { get; set; } = default!;
        public string EntityId { get; set; } = default!;
        public EntityState State { get; set; }
        public Guid OrganizationId { get; set; }
        public string CorrelationId { get; set; } = default!;
        public Dictionary<string, object?>? OldValues { get; set; }
        public Dictionary<string, object?>? NewValues { get; set; }
        public List<string>? ChangedProperties { get; set; }
    }
}
