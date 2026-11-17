using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using MyApp.Application.DTOs.Help;
using MyApp.Application.Logging;
using MyApp.Domain.Constants;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using Newtonsoft.Json;

namespace MyApp.Application.Help;

/// <summary>
/// Documentation gaps (manual.md 7.4): recording with masking, and the Admin export. A record holds no user, no
/// user name and no organization.
/// </summary>
public class HelpGapService(
    ILogger<HelpGapService> logger,
    IHelpGapsRepository gaps,
    IOrganizationsRepository organizations,
    HelpSettings settings,
    TimeProvider timeProvider)
{
    public const string TierRetrieval = "retrieval";
    public const string TierBrowser = "browser";
    public const string TierServer = "server";

    private static readonly string[] Tiers = [TierRetrieval, TierBrowser, TierServer];

    /// <summary>
    /// Validates and stores one record, and writes the keyed entries of an unusable or too slow answer. With recording
    /// switched off the record is dropped, the entries are still written.
    /// </summary>
    // US-HELP-04, US-HELP-05, US-HELP-06
    public async Task RecordAsync(HelpGapDto dto, string route, CancellationToken ct = default)
    {
        var lang = (dto.Lang ?? string.Empty).Trim().ToLowerInvariant();
        if (lang.Length is < 2 or > 10 || !lang.All(c => char.IsAsciiLetter(c) || c == '-'))
            throw new ValidationException(new ValidationResult("The language is not valid.", ["lang"]), null, null);

        var outcome = dto.Outcome ?? string.Empty;
        if (!HelpGapOutcomes.All.Contains(outcome))
            throw new ValidationException(new ValidationResult("The outcome is not valid.", ["outcome"]), null, null);

        var tier = dto.Tier ?? TierRetrieval;
        if (!Tiers.Contains(tier))
            throw new ValidationException(new ValidationResult("The tier is not valid.", ["tier"]), null, null);

        var question = GapMasking.Mask(dto.Question ?? string.Empty).Trim();
        if (question.Length == 0)
            throw new ValidationException(new ValidationResult("The question is empty.", ["question"]), null, null);

        // The keyed entries of the answer outcomes have one call site, here, whichever tier answered
        if (outcome is HelpGapOutcomes.Refused or HelpGapOutcomes.Invalid)
            logger.HelpAnswerDeclined(tier, outcome);
        else if (outcome == HelpGapOutcomes.Timeout)
            logger.HelpAnswerTimeout(tier);

        if (!settings.GapsEnabled)
            return;

        var topSections = (dto.TopSections ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim())
            .Where(s => s.Length <= 200).Take(10).ToList();

        await gaps.AddAsync(new HelpGap
        {
            Id = Guid.NewGuid(),
            // The server's clock: a device clock can be wrong, and the retention and the period of the export use this time
            RecordedAt = timeProvider.GetUtcNow().UtcDateTime,
            Lang = lang,
            Route = string.IsNullOrWhiteSpace(route) ? "/" : AssistantProtocol.CutCodePoints(route, 200),
            Question = question,
            Outcome = outcome,
            Tier = tier,
            ProjectVersion = Limit(dto.Pack?.ProjectVersion, 50),
            PackSha256 = Limit(dto.Pack?.Sha256, 64),
            TopSections = topSections.Count > 0 ? JsonConvert.SerializeObject(topSections) : null,
            Coverage = dto.Coverage is { } c && c is >= 0 and <= 1 ? c : null
        }, ct);
    }

    /// <summary>
    /// The records of a period for an Admin of any organization, oldest first. A <c>to</c> without a time of day
    /// covers the whole day. The entries carry no user and no organization.
    /// </summary>
    // US-HELP-07
    public async Task<List<HelpGapDto>> ListAsync(Guid userAadId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var memberships = await organizations.GetByUserAadIdAsync(userAadId, ct);
        var isAdmin = memberships.Any(o => o.Users.Any(u => u.User?.AadId == userAadId
                                                            && u.Status == OrganizationUserStatus.Active
                                                            && u.Role == OrganizationRole.Admin));
        if (!isAdmin)
            throw new UnauthorizedAccessException("Only Admin can download the unanswered questions.");

        if (from.HasValue && to.HasValue && to.Value.Date < from.Value.Date)
            throw new ValidationException(
                new ValidationResult("The end date cannot be before the start date.", ["to"]), null, null);

        var fromUtc = from.HasValue ? ToUtc(from.Value) : (DateTime?)null;
        DateTime? toExclusive = null;
        if (to.HasValue)
        {
            var toUtc = ToUtc(to.Value);
            toExclusive = toUtc.TimeOfDay == TimeSpan.Zero ? toUtc.AddDays(1) : toUtc.AddTicks(1);
        }

        var items = await gaps.ListAsync(fromUtc, toExclusive, ct);
        return items.Select(g => new HelpGapDto
        {
            SchemaVersion = 1,
            RecordedAt = DateTime.SpecifyKind(g.RecordedAt, DateTimeKind.Utc),
            Lang = g.Lang,
            Route = g.Route,
            Question = g.Question,
            Outcome = g.Outcome,
            Tier = g.Tier,
            Pack = new HelpGapPackDto { ProjectVersion = g.ProjectVersion, Sha256 = g.PackSha256 },
            TopSections = g.TopSections is null ? [] : JsonConvert.DeserializeObject<List<string>>(g.TopSections) ?? [],
            Coverage = g.Coverage
        }).ToList();
    }

    /// <summary>The oldest <c>RecordedAt</c> that is kept (<c>MyApp:Help:Gaps:RetentionDays</c>).</summary>
    public static DateTime RetentionCutoff(DateTime utcNow, int retentionDays) => utcNow.AddDays(-retentionDays);

    private static string? Limit(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : AssistantProtocol.CutCodePoints(value.Trim(), max);

    private static DateTime ToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
