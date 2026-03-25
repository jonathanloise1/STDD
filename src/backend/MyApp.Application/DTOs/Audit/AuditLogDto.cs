namespace MyApp.Application.DTOs.Audit;

/// <summary>
/// DTO for AuditLog list view.
/// <userstory ref="US-AUD-02, US-AUD-03" />
/// </summary>
public class AuditLogDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = default!;
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
