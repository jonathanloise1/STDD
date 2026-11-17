using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using MyApp.Application.DTOs.Help;
using MyApp.Application.Help;
using MyApp.Application.Logging;
using MyApp.Domain.Constants;
using MyApp.Domain.Entities;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

/// <summary>Documentation gaps (manual.md 7.4): recording, masking, retention and the Admin export.</summary>
[TestFixture]
[Property("UserStory", "US-HELP-06")]
[Property("UserStory", "US-HELP-07")]
public class HelpGapServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 3, 10, 9, 30, 0, TimeSpan.Zero);

    private FakeHelpGapsRepository _repo = null!;
    private CapturingLogger<HelpGapService> _log = null!;
    private TestTimeProvider _clock = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = new FakeHelpGapsRepository();
        _log = new CapturingLogger<HelpGapService>();
        _clock = new TestTimeProvider(Now);
    }

    private HelpGapService Create(FakeOrganizationsRepository? organizations = null, bool gapsEnabled = true) =>
        new(_log, _repo, organizations ?? new FakeOrganizationsRepository(), new HelpSettings { GapsEnabled = gapsEnabled }, _clock);

    private static HelpGapDto Dto(string question = "can i get an email when someone assigns me a task", string outcome = "no_match", string? tier = "retrieval") => new()
    {
        Lang = "en",
        Route = "/tasks",
        Question = question,
        Outcome = outcome,
        Tier = tier,
        Pack = new HelpGapPackDto { ProjectVersion = "0.1.0", Sha256 = new string('a', 64) },
        TopSections = ["tasks/US-TASK-01#overview", "tasks/US-TASK-02#overview"],
        Coverage = 0.22
    };

    private static (FakeOrganizationsRepository Orgs, Guid Admin, Guid Editor, Guid Viewer) Team()
    {
        var (org, members) = FakeOrganizationsRepository.Build(("a", OrganizationRole.Admin), ("e", OrganizationRole.Editor), ("v", OrganizationRole.Viewer));
        return (new FakeOrganizationsRepository(org), members["a"].User!.AadId, members["e"].User!.AadId, members["v"].User!.AadId);
    }

    // ---------------------------------------------------------------- recording

    [Test]
    public async Task Record_StoresTheMaskedQuestionWithoutAnyIdentity()
    {
        await Create().RecordAsync(Dto("can i pay invoice 12345678, write to anna.rossi@example.com or see https://example.com/pay"), "/tasks");

        var gap = _repo.Items.Single();
        Assert.That(gap.Question, Is.EqualTo("can i pay invoice [number], write to [email] or see [url]"));
        Assert.That(gap.Lang, Is.EqualTo("en"));
        Assert.That(gap.Route, Is.EqualTo("/tasks"));
        Assert.That(gap.Outcome, Is.EqualTo("no_match"));
        Assert.That(gap.Tier, Is.EqualTo("retrieval"));
        Assert.That(gap.ProjectVersion, Is.EqualTo("0.1.0"));
        Assert.That(gap.TopSections, Is.EqualTo("[\"tasks/US-TASK-01#overview\",\"tasks/US-TASK-02#overview\"]"));
        Assert.That(gap.Coverage, Is.EqualTo(0.22));
        Assert.That(typeof(HelpGap).GetProperties().Select(p => p.Name),
            Has.None.Matches<string>(n => n.Contains("User", StringComparison.Ordinal) || n.Contains("Organization", StringComparison.Ordinal)
                                          || n.Contains("Created", StringComparison.Ordinal)));
    }

    [Test]
    public async Task Record_UsesTheServerClockNotTheOneOfTheDevice()
    {
        var dto = Dto();
        dto.RecordedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await Create().RecordAsync(dto, "/tasks");

        Assert.That(_repo.Items.Single().RecordedAt, Is.EqualTo(Now.UtcDateTime));
    }

    [Test]
    public async Task Record_CutsALongQuestionAtThreeHundredCharactersAfterMasking()
    {
        await Create().RecordAsync(Dto(new string('q', 500)), "/tasks");

        Assert.That(_repo.Items.Single().Question, Has.Length.EqualTo(300));
    }

    [Test]
    public async Task Record_NotHelpfulFromAnyTier_IsStoredWithoutAKeyedEntry()
    {
        await Create().RecordAsync(Dto(outcome: "not_helpful", tier: "browser"), "/tasks");

        Assert.That(_repo.Items.Single().Outcome, Is.EqualTo("not_helpful"));
        Assert.That(_repo.Items.Single().Tier, Is.EqualTo("browser"));
        Assert.That(_log.Entries, Is.Empty);
    }

    [TestCase("refused", "browser")]
    [TestCase("invalid", "server")]
    public async Task Record_ADeclinedAnswer_WritesTheDeclinedEntry(string outcome, string tier)
    {
        await Create().RecordAsync(Dto(outcome: outcome, tier: tier), "/tasks");

        var entry = _log.Entries.Single();
        Assert.That(entry.LogKey, Is.EqualTo(HelpLog.Keys.HelpAnswerDeclined));
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Information));
        Assert.That(entry["Tier"], Is.EqualTo(tier));
        Assert.That(entry["Outcome"], Is.EqualTo(outcome));
        Assert.That(_repo.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Record_ATimeout_WritesTheTimeoutEntryAtWarning()
    {
        await Create().RecordAsync(Dto(outcome: "timeout", tier: "server"), "/tasks");

        var entry = _log.Entries.Single();
        Assert.That(entry.LogKey, Is.EqualTo(HelpLog.Keys.HelpAnswerTimeout));
        Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(entry["Tier"], Is.EqualTo("server"));
    }

    [Test]
    public async Task Record_NoMatch_WritesNoKeyedEntry()
    {
        await Create().RecordAsync(Dto(), "/tasks");

        Assert.That(_log.Entries, Is.Empty);
    }

    [Test]
    public async Task Record_NoEntryCarriesTheQuestion()
    {
        await Create().RecordAsync(Dto("secret question about anna", "refused", "server"), "/tasks");

        Assert.That(_log.Entries.Single().Message, Does.Not.Contain("secret"));
        Assert.That(_log.Entries.Single().State.Values.Select(v => v?.ToString()), Has.None.Contains("secret"));
    }

    [Test]
    public async Task Record_RecordingSwitchedOff_StoresNothingButStillWritesTheEntries()
    {
        await Create(gapsEnabled: false).RecordAsync(Dto(outcome: "refused", tier: "server"), "/tasks");

        Assert.That(_repo.Items, Is.Empty);
        Assert.That(_log.Entries.Single().LogKey, Is.EqualTo(HelpLog.Keys.HelpAnswerDeclined));
    }

    [TestCase("", "no_match", "retrieval", "q")]
    [TestCase("e", "no_match", "retrieval", "q")]
    [TestCase("en", "bogus", "retrieval", "q")]
    [TestCase("en", "no_match", "cloud", "q")]
    [TestCase("en", "no_match", "retrieval", "   ")]
    public void Record_InvalidFields_AreRejected(string lang, string outcome, string tier, string question)
    {
        var dto = Dto(question, outcome, tier);
        dto.Lang = lang;

        Assert.ThrowsAsync<ValidationException>(() => Create().RecordAsync(dto, "/tasks"));
        Assert.That(_repo.Items, Is.Empty);
    }

    [Test]
    public async Task Record_QuestionThatIsOnlyAnAddress_IsStoredMasked()
    {
        await Create().RecordAsync(Dto("anna@example.com"), "/tasks");

        Assert.That(_repo.Items.Single().Question, Is.EqualTo("[email]"));
    }

    [Test]
    public async Task Record_IgnoresAMissingTierAndCoverageOutOfRange()
    {
        var dto = Dto(tier: null);
        dto.Coverage = 7;

        await Create().RecordAsync(dto, "");

        var gap = _repo.Items.Single();
        Assert.That(gap.Tier, Is.EqualTo("retrieval"));
        Assert.That(gap.Coverage, Is.Null);
        Assert.That(gap.Route, Is.EqualTo("/"));
    }

    // ---------------------------------------------------------------- export

    private void Seed(DateTime at, string question = "q") => _repo.Items.Add(new HelpGap
    {
        Id = Guid.NewGuid(), RecordedAt = at, Lang = "en", Route = "/tasks", Question = question, Outcome = "no_match", Tier = "retrieval",
        ProjectVersion = "0.1.0", PackSha256 = "abc", TopSections = "[\"s1\"]", Coverage = 0.5
    });

    [Test]
    public async Task List_AnAdminGetsTheRecordsOfThePeriodWithoutIdentity()
    {
        var (orgs, admin, _, _) = Team();
        Seed(new DateTime(2030, 1, 5, 10, 0, 0, DateTimeKind.Utc), "inside");
        Seed(new DateTime(2030, 1, 31, 23, 59, 0, DateTimeKind.Utc), "last minute");
        Seed(new DateTime(2030, 2, 1, 0, 0, 0, DateTimeKind.Utc), "after");
        Seed(new DateTime(2029, 12, 31, 23, 59, 0, DateTimeKind.Utc), "before");

        var list = await Create(orgs).ListAsync(admin, new DateTime(2030, 1, 1), new DateTime(2030, 1, 31));

        Assert.That(list.Select(g => g.Question), Is.EqualTo(new[] { "inside", "last minute" }));
        var first = list[0];
        Assert.That(first.SchemaVersion, Is.EqualTo(1));
        Assert.That(first.Lang, Is.EqualTo("en"));
        Assert.That(first.Route, Is.EqualTo("/tasks"));
        Assert.That(first.Outcome, Is.EqualTo("no_match"));
        Assert.That(first.RecordedAt, Is.EqualTo(new DateTime(2030, 1, 5, 10, 0, 0, DateTimeKind.Utc)));
        Assert.That(first.Pack!.ProjectVersion, Is.EqualTo("0.1.0"));
        Assert.That(first.TopSections, Is.EqualTo(new[] { "s1" }));
        Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(first), Does.Not.Contain("rganization").And.Not.Contain("user").And.Not.Contain("User"));
    }

    [Test]
    public async Task List_APeriodWithNoQuestions_IsAnEmptyArray()
    {
        var (orgs, admin, _, _) = Team();
        Seed(new DateTime(2030, 1, 5, 0, 0, 0, DateTimeKind.Utc));

        var list = await Create(orgs).ListAsync(admin, new DateTime(2001, 1, 1), new DateTime(2001, 1, 31));

        Assert.That(list, Is.Empty);
    }

    [Test]
    public void List_EndBeforeStart_IsAFieldError()
    {
        var (orgs, admin, _, _) = Team();

        var e = Assert.ThrowsAsync<ValidationException>(() => Create(orgs).ListAsync(admin, new DateTime(2030, 2, 1), new DateTime(2030, 1, 1)));

        Assert.That(e!.ValidationResult.ErrorMessage, Is.EqualTo("The end date cannot be before the start date."));
        Assert.That(e.ValidationResult.MemberNames, Is.EqualTo(new[] { "to" }));
    }

    [Test]
    public void List_OneDayPeriod_IsAllowed()
    {
        var (orgs, admin, _, _) = Team();

        Assert.DoesNotThrowAsync(() => Create(orgs).ListAsync(admin, new DateTime(2030, 1, 1), new DateTime(2030, 1, 1)));
    }

    [Test]
    public void List_EditorViewerAndStranger_AreRefused()
    {
        var (orgs, _, editor, viewer) = Team();

        foreach (var user in new[] { editor, viewer, Guid.NewGuid() })
            Assert.ThrowsAsync<UnauthorizedAccessException>(() => Create(orgs).ListAsync(user, null, null));
    }

    [Test]
    public void List_AnAdminWhoWasDisabled_IsRefused()
    {
        var (org, members) = FakeOrganizationsRepository.Build(("a", OrganizationRole.Admin));
        members["a"].Status = OrganizationUserStatus.Disabled;

        Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Create(new FakeOrganizationsRepository(org)).ListAsync(members["a"].User!.AadId, null, null));
    }

    [Test]
    public async Task List_AnAdminOfAnyOrganizationMaySee_EvenIfOnlyEditorInAnother()
    {
        var (orgA, a) = FakeOrganizationsRepository.Build(("u", OrganizationRole.Editor));
        var user = a["u"].User!;
        var orgB = new Organization { Id = Guid.NewGuid(), Name = "B" };
        orgB.Users.Add(new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = orgB.Id, UserId = user.Id, User = user, Email = user.Email, FullName = "u", Role = OrganizationRole.Admin, Status = OrganizationUserStatus.Active });

        var list = await Create(new FakeOrganizationsRepository(orgA, orgB)).ListAsync(user.AadId, null, null);

        Assert.That(list, Is.Empty);
    }

    // ---------------------------------------------------------------- retention

    [Test]
    public async Task Retention_DeletesTheRecordsOlderThanTheConfiguredDays()
    {
        Seed(Now.UtcDateTime.AddDays(-91), "old");
        Seed(Now.UtcDateTime.AddDays(-89), "recent");

        var deleted = await _repo.DeleteOlderThanAsync(HelpGapService.RetentionCutoff(Now.UtcDateTime, 90));

        Assert.That(deleted, Is.EqualTo(1));
        Assert.That(_repo.Items.Select(g => g.Question), Is.EqualTo(new[] { "recent" }));
        Assert.That(HelpGapService.RetentionCutoff(Now.UtcDateTime, 30), Is.EqualTo(Now.UtcDateTime.AddDays(-30)));
    }
}
