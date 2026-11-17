using Microsoft.Extensions.Logging.Abstractions;
using MyApp.Application.Services;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

[TestFixture]
[Property("UserStory", "US-AUD-01")]
[Property("UserStory", "US-AUD-02")]
public class AuditServiceTests
{
    private sealed class CapturingAuditRepository : IAuditLogsRepository
    {
        public DateTime NotBefore;
        public DateTime? From;
        public DateTime? ToExclusive;

        public Task<(List<AuditLog> Items, int TotalCount)> GetByOrganizationIdAsync(Guid organizationId, DateTime notBefore,
            DateTime? from, DateTime? toExclusive, string? entityType, Guid? userId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            (NotBefore, From, ToExclusive) = (notBefore, from, toExclusive);
            return Task.FromResult((new List<AuditLog>(), 0));
        }

        public Task<List<AuditLog>> GetByEntityAsync(string entityType, string entityId, CancellationToken cancellationToken = default) => Task.FromResult(new List<AuditLog>());
        public Task<List<AuditLog>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult(new List<AuditLog>());
        public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<AuditLog> auditLogs, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static (AuditService Service, CapturingAuditRepository Repo, Guid Admin, Guid Org) Setup(OrganizationRole role = OrganizationRole.Admin)
    {
        var (org, members) = FakeOrganizationsRepository.Build(("member", role));
        var repo = new CapturingAuditRepository();
        var clock = new TestTimeProvider(new DateTimeOffset(2030, 1, 10, 9, 0, 0, TimeSpan.Zero));
        return (new AuditService(NullLogger<AuditService>.Instance, repo, new FakeOrganizationsRepository(org), clock),
            repo, members["member"].User!.AadId, org.Id);
    }

    [Test]
    public async Task TheListIsLimitedToTheLastYearInTheQuery()
    {
        var (service, repo, admin, org) = Setup();

        await service.GetByOrganizationAsync(admin, org, null, null, null, null);

        Assert.That(repo.NotBefore, Is.EqualTo(new DateTime(2029, 1, 10, 9, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public async Task AToDateCoversTheWholeDay()
    {
        var (service, repo, admin, org) = Setup();

        await service.GetByOrganizationAsync(admin, org, new DateTime(2030, 1, 2), new DateTime(2030, 1, 5), null, null);

        Assert.That(repo.From, Is.EqualTo(new DateTime(2030, 1, 2, 0, 0, 0, DateTimeKind.Utc)));
        Assert.That(repo.ToExclusive, Is.EqualTo(new DateTime(2030, 1, 6, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public async Task AToInstantWithATimeIsInclusive()
    {
        var (service, repo, admin, org) = Setup();

        await service.GetByOrganizationAsync(admin, org, null, new DateTime(2030, 1, 5, 21, 59, 59, DateTimeKind.Utc), null, null);

        Assert.That(repo.ToExclusive, Is.GreaterThan(new DateTime(2030, 1, 5, 21, 59, 59, DateTimeKind.Utc)));
        Assert.That(repo.ToExclusive, Is.LessThan(new DateTime(2030, 1, 5, 22, 0, 0, DateTimeKind.Utc)));
    }

    [Test]
    public async Task TheTypeChoicesAreFixedWithFriendlyNames()
    {
        var (service, _, admin, org) = Setup();

        var types = await service.GetEntityTypesAsync(admin, org);

        Assert.That(types.Select(t => t.Name), Is.EqualTo(new[] { "Task", "Team member", "Organization", "User" }));
    }

    [Test]
    public void OnlyAdminsMayListTheChanges()
    {
        var (service, _, editor, org) = Setup(OrganizationRole.Editor);

        Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetByOrganizationAsync(editor, org, null, null, null, null));
    }
}
