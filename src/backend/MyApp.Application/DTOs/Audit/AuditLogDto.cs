namespace MyApp.Application.DTOs.Audit;

/// <summary>
/// DTO for AuditLog list view.
/// </summary>
// US-AUD-01, US-AUD-03
public class AuditLogDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = default!;

    /// <summary>The friendly type name: "Task", "Team member", "Organization" or "User".</summary>
    public string EntityTypeName { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string Action { get; set; } = default!;
    public Guid UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? ChangedProperties { get; set; }
    public DateTime Timestamp { get; set; }
    public string? CorrelationId { get; set; }
}
