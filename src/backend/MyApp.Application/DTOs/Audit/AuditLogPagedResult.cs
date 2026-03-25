namespace MyApp.Application.DTOs.Audit;

/// <summary>
/// Paginated response for audit log queries.
/// <userstory ref="US-AUD-02" />
/// </summary>
public class AuditLogPagedResult
{
    public List<AuditLogDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
