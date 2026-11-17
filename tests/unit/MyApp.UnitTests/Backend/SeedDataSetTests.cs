using MyApp.Domain.Constants;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Seed;
using MyApp.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

[TestFixture]
public class SeedDataSetTests
{
    private static readonly DateTime Now = new(2030, 1, 10, 9, 0, 0, DateTimeKind.Utc);

    [Test]
    public void TheSameClockGivesTheSameData()
    {
        string Fingerprint(DateTime now) => string.Join("|",
            SeedDataSet.Users().Select(u => $"{u.AadId}{u.Email}")
                .Concat(SeedDataSet.Organizations(now).Select(o => $"{o.Id}{o.Name}{o.CreatedAt:O}"))
                .Concat(SeedDataSet.Memberships().Select(m => $"{m.Id}{m.Role}{m.Status}"))
                .Concat(SeedDataSet.Tasks(now).Select(t => $"{t.Id}{t.Title}{t.Status}{t.DueDate}{t.CreatedAt:O}"))
                .Concat(SeedDataSet.AuditEntries(now).Select(a => $"{a.Id}{a.EntityId}{a.Timestamp:O}{a.NewValues}{a.CorrelationId}")));

        Assert.That(Fingerprint(Now), Is.EqualTo(Fingerprint(Now)));
        Assert.That(Fingerprint(Now.AddDays(1)), Is.Not.EqualTo(Fingerprint(Now)));
    }

    [Test]
    public void TheUserRowIdDiffersFromTheAadId_SoAMixUpCannotHide()
    {
        foreach (var user in SeedDataSet.Users())
            Assert.That(SeedDataSet.RowId(user.AadId), Is.Not.EqualTo(user.AadId));
        Assert.That(SeedDataSet.Users().Select(u => SeedDataSet.RowId(u.AadId)).Distinct().Count(), Is.EqualTo(SeedDataSet.Users().Count));
    }

    [Test]
    public void KeepsTheFixedIdsOfTheE2EUsers()
    {
        string Id(long n) => SeedDataSet.G(n).ToString();
        Assert.That(SeedDataSet.DevUser.ToString(), Is.EqualTo("00000000-0000-0000-0000-000000000001"));
        Assert.That(Id(2), Is.EqualTo(SeedDataSet.DevMember.ToString()).And.EqualTo("00000000-0000-0000-0000-000000000002"));
        Assert.That(SeedDataSet.UserC.ToString(), Is.EqualTo("00000000-0000-0000-0000-000000000003"));
        Assert.That(SeedDataSet.NewUser.ToString(), Is.EqualTo("00000000-0000-0000-0000-000000000004"));
        Assert.That(SeedDataSet.DevViewer.ToString(), Is.EqualTo("00000000-0000-0000-0000-000000000005"));
        Assert.That(SeedDataSet.DevOrg.ToString(), Is.EqualTo("00000000-0000-0000-0000-000000000100"));
        Assert.That(SeedDataSet.InvitedOrg.ToString(), Is.EqualTo("00000000-0000-0000-0000-000000000200"));
    }

    [Test]
    public void IdsAreUnique()
    {
        var ids = SeedDataSet.Memberships().Select(m => m.Id)
            .Concat(SeedDataSet.Tasks(Now).Select(t => t.Id))
            .Concat(SeedDataSet.AuditEntries(Now).Select(a => a.Id))
            .Concat(SeedDataSet.Organizations(Now).Select(o => o.Id));
        Assert.That(ids, Is.Unique);
        Assert.That(SeedDataSet.Users().Select(u => u.Email.ToLowerInvariant()), Is.Unique);
    }

    [Test]
    public void DevOrg_HasOneAdminTheThreeSignInUsersAndThreeViewersWhoNeverSignIn()
    {
        var members = SeedDataSet.Memberships().Where(m => m.OrganizationId == SeedDataSet.DevOrg).ToList();

        Assert.That(members.Where(m => m.Role == OrganizationRole.Admin).Select(m => m.UserAadId), Is.EqualTo(new Guid?[] { SeedDataSet.DevUser }));
        Assert.That(members.Single(m => m.UserAadId == SeedDataSet.DevMember).Role, Is.EqualTo(OrganizationRole.Editor));
        Assert.That(members.Single(m => m.UserAadId == SeedDataSet.DevViewer).Role, Is.EqualTo(OrganizationRole.Viewer));
        Assert.That(members.Select(m => m.FullName), Is.SupersetOf(new[] { "Spare Member", "Leaving Member", "Former Member" }));
        Assert.That(members.Where(m => m.FullName.EndsWith(" Member") && m.UserAadId != SeedDataSet.DevMember).Select(m => m.Role), Is.All.EqualTo(OrganizationRole.Viewer));
        Assert.That(members, Has.None.Matches<SeedMembership>(m => m.Status != OrganizationUserStatus.Active));
    }

    [Test]
    public void DevOrg_HasNineDemoTasks_TheMonitoringTaskNewestAndWithoutDueDate()
    {
        var tasks = SeedDataSet.Tasks(Now).Where(t => t.OrganizationId == SeedDataSet.DevOrg).ToList();

        Assert.That(tasks, Has.Count.EqualTo(9));
        Assert.That(tasks.MaxBy(t => t.CreatedAt)!.Title, Is.EqualTo("Set up monitoring dashboard"));
        Assert.That(tasks.Single(t => t.Title == "Set up monitoring dashboard").DueDate, Is.Null);
        Assert.That(tasks.Single(t => t.Title == "Cancelled: Migrate to PostgreSQL").Status, Is.EqualTo(TaskItemStatus.Cancelled));
        Assert.That(tasks.Select(t => t.CreatedAt), Is.Unique);
    }

    [Test]
    public void StatsOrg_HasTwelveTasksWithTheCountsOfTheDashboardCases()
    {
        var tasks = SeedDataSet.Tasks(Now).Where(t => t.OrganizationId == SeedDataSet.StatsOrg).OrderBy(t => t.CreatedAt).ToList();

        Assert.That(tasks.Select(t => t.Title), Is.EqualTo(Enumerable.Range(1, 12).Select(i => $"Stats task {i:00}")));
        Assert.That(tasks.Count(t => t.Status == TaskItemStatus.Todo), Is.EqualTo(5));
        Assert.That(tasks.Count(t => t.Status == TaskItemStatus.InProgress), Is.EqualTo(3));
        Assert.That(tasks.Count(t => t.Status == TaskItemStatus.Done), Is.EqualTo(3));
        Assert.That(tasks.Count(t => t.Status == TaskItemStatus.Cancelled), Is.EqualTo(1));
        Assert.That(tasks.Where(t => t.DueDate is null).Select(t => t.Title), Is.EqualTo(new[] { "Stats task 12" }));
        Assert.That(tasks.Select(t => t.CreatedAt.Date), Is.Unique);
    }

    [Test]
    public void EmptyOrg_HasOneAdminNoTasksAndNoAudit_AndTheOtherOrganizationsMatchTheSuites()
    {
        var memberships = SeedDataSet.Memberships();

        Assert.That(memberships.Where(m => m.OrganizationId == SeedDataSet.EmptyOrg).Select(m => m.UserAadId), Is.EqualTo(new Guid?[] { SeedDataSet.EmptyAdmin }));
        Assert.That(SeedDataSet.Tasks(Now).Any(t => t.OrganizationId == SeedDataSet.EmptyOrg), Is.False);
        Assert.That(SeedDataSet.AuditEntries(Now).Any(a => a.OrganizationId == SeedDataSet.EmptyOrg), Is.False);

        var invite = memberships.Where(m => m.OrganizationId == SeedDataSet.InvitedOrg).ToList();
        Assert.That(invite.Single(m => m.Status == OrganizationUserStatus.Pending).Email, Is.EqualTo("testc@myapp.test"));
        Assert.That(invite.Single(m => m.Status == OrganizationUserStatus.Pending).Role, Is.EqualTo(OrganizationRole.Editor));
        Assert.That(invite.Single(m => m.UserAadId == SeedDataSet.InviteAdmin).Role, Is.EqualTo(OrganizationRole.Admin));

        var stats = memberships.Where(m => m.OrganizationId == SeedDataSet.StatsOrg).ToList();
        Assert.That(stats.Single(m => m.UserAadId == SeedDataSet.StatsViewer).Role, Is.EqualTo(OrganizationRole.Viewer));

        var inNoOrg = new[] { SeedDataSet.NewUser, SeedDataSet.SetupUser };
        Assert.That(memberships.Where(m => inNoOrg.Contains(m.UserAadId ?? Guid.Empty)), Is.Empty);
        Assert.That(SeedDataSet.Users().Where(u => inNoOrg.Contains(u.AadId)).Select(u => u.HasCompletedOnboarding), Is.All.False);
    }

    [Test]
    public void DevOrgAudit_HasAtLeastThirtyChangesOfTaskAndTeamMemberByDevUserWithinTheLastMonth()
    {
        var entries = SeedDataSet.AuditEntries(Now);

        Assert.That(entries.Count, Is.GreaterThanOrEqualTo(30));
        Assert.That(entries.Select(e => e.OrganizationId), Is.All.EqualTo(SeedDataSet.DevOrg));
        Assert.That(entries.Select(e => e.EntityType).Distinct(), Is.EquivalentTo(new[] { AuditEntityTypes.Task, AuditEntityTypes.TeamMember }));
        Assert.That(entries.Select(e => e.UserAadId), Is.All.EqualTo(SeedDataSet.DevUser));
        Assert.That(entries.Select(e => e.UserDisplayName), Is.All.EqualTo("Dev User"));
        Assert.That(entries.Select(e => e.Timestamp), Is.All.InRange(Now.AddDays(-30), Now));
        Assert.That(entries.Select(e => e.Timestamp), Is.Unique);
        Assert.That(entries.Where(e => e.Action == AuditAction.Create).Select(e => e.OldValues), Is.All.Null);
        Assert.That(entries.Where(e => e.Action == AuditAction.Delete).Select(e => e.NewValues), Is.All.Null);
        Assert.That(entries.Where(e => e.Action == AuditAction.Update).Select(e => e.ChangedProperties), Is.All.Not.Null);
    }

    [Test]
    public void TheFixedClock_IsHonouredInDevelopmentOnly()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [ClockFactory.FixedUtcNowKey] = "2030-01-10T09:00:00Z" }).Build();

        var development = ClockFactory.Create(config, new TestHostEnvironment("Development")).GetUtcNow();
        var production = ClockFactory.Create(config, new TestHostEnvironment("Production")).GetUtcNow();

        Assert.That(development, Is.EqualTo(new DateTimeOffset(2030, 1, 10, 9, 0, 0, TimeSpan.Zero)).Within(TimeSpan.FromMinutes(1)));
        Assert.That(production.Year, Is.Not.EqualTo(2030));
    }
}
