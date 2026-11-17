namespace MyApp.Application.DTOs.Audit;

/// <summary>A choice of the "Type" field of the Audit Log page.</summary>
// US-AUD-02
public class AuditEntityTypeDto
{
    /// <summary>The value to send as the <c>entityType</c> filter.</summary>
    public string Value { get; set; } = default!;

    /// <summary>The friendly name shown to the user.</summary>
    public string Name { get; set; } = default!;
}
