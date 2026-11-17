using System.ComponentModel.DataAnnotations;

namespace MyApp.Domain.Entities;

/// <summary>
/// A question that the documentation did not answer (manual.md 7.4). It holds no user, no user name and no
/// organization, and its question is already masked.
/// </summary>
// US-HELP-07
public class HelpGap
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>UTC time the backend received the record.</summary>
    public DateTime RecordedAt { get; set; }

    /// <summary>Language of the manual the question was asked in (<c>en</c>, <c>it</c>).</summary>
    [Required, MaxLength(10)]
    public string Lang { get; set; } = default!;

    /// <summary>Route template of the screen the help was opened on, without identifiers.</summary>
    [Required, MaxLength(200)]
    public string Route { get; set; } = default!;

    /// <summary>The question after masking, at most 300 characters.</summary>
    [Required, MaxLength(300)]
    public string Question { get; set; } = default!;

    /// <summary>One of <see cref="Constants.HelpGapOutcomes"/>.</summary>
    [Required, MaxLength(20)]
    public string Outcome { get; set; } = default!;

    /// <summary><c>retrieval</c>, <c>browser</c> or <c>server</c>.</summary>
    [Required, MaxLength(20)]
    public string Tier { get; set; } = default!;

    [MaxLength(50)]
    public string? ProjectVersion { get; set; }

    [MaxLength(64)]
    public string? PackSha256 { get; set; }

    /// <summary>JSON array of the ids of the best sections.</summary>
    [MaxLength(2000)]
    public string? TopSections { get; set; }

    /// <summary>Share of the question terms found in the best section, 0 to 1.</summary>
    public double? Coverage { get; set; }
}
