namespace MyApp.Application.DTOs.Tasks;

/// <summary>The task counts of the Dashboard page, over every task of the organization.</summary>
// US-DASH-01
public class DashboardCountsDto
{
    /// <summary>Every task, whatever its status (Cancelled included).</summary>
    public int Total { get; set; }
    public int Todo { get; set; }
    public int InProgress { get; set; }
    public int Done { get; set; }
    public int Cancelled { get; set; }
}
