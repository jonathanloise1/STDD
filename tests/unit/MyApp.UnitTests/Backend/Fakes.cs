using MyApp.Domain.Constants;
using MyApp.Domain.Entities;
using MyApp.Domain.Repositories;

namespace MyApp.UnitTests.Backend;

/// <summary>An organization held in memory.</summary>
internal sealed class FakeOrganizationsRepository(params Organization[] organizations) : IOrganizationsRepository
{
    public List<Organization> Items { get; } = [.. organizations];
    public int Updates { get; private set; }

    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(o => o.Id == id));

    public Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        Items.Add(organization);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        Updates++;
        return Task.CompletedTask;
    }

    public Task<List<Organization>> GetByUserAadIdAsync(Guid userAadId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(o => o.Users.Any(u => u.User?.AadId == userAadId)).ToList());

    public Task<List<OrganizationUser>> ActivatePendingMembershipsByEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<OrganizationUser>());

    public Task<List<Organization>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(o => o.Users.Any(u => u.UserId == userId)).ToList());

    public Task AddOrganizationUserAsync(OrganizationUser orgUser, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>An organization with one member per role given, each with a user.</summary>
    public static (Organization Org, Dictionary<string, OrganizationUser> Members) Build(params (string Name, OrganizationRole Role)[] members)
    {
        var org = new Organization { Id = Guid.NewGuid(), Name = "Org" };
        var map = new Dictionary<string, OrganizationUser>();
        foreach (var (name, role) in members)
        {
            var user = new User { Id = Guid.NewGuid(), AadId = Guid.NewGuid(), FirstName = name, LastName = "Test", Email = $"{name}@test" };
            var membership = new OrganizationUser
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id, UserId = user.Id, User = user,
                Email = user.Email, FullName = name, Role = role, Status = OrganizationUserStatus.Active
            };
            org.Users.Add(membership);
            map[name] = membership;
        }
        return (org, map);
    }
}

/// <summary>Tasks held in memory.</summary>
internal sealed class FakeTaskItemsRepository : ITaskItemsRepository
{
    public List<TaskItem> Items { get; } = [];

    public Task<(List<TaskItem> Items, int TotalCount)> GetByOrganizationIdAsync(Guid organizationId, string? statusFilter,
        Guid? assignedToUserId, string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var list = Items.Where(t => t.OrganizationId == organizationId).ToList();
        return Task.FromResult((list, list.Count));
    }

    public Task<TaskItem?> GetByIdAsync(Guid organizationId, Guid taskId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(t => t.Id == taskId && t.OrganizationId == organizationId));

    public Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        if (task.Id == Guid.Empty) task.Id = Guid.NewGuid();
        Items.Add(task);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DeleteAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        Items.Remove(task);
        return Task.CompletedTask;
    }

    public Task<Dictionary<TaskItemStatus, int>> CountByStatusAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(t => t.OrganizationId == organizationId).GroupBy(t => t.Status).ToDictionary(g => g.Key, g => g.Count()));

    public Task<List<TaskItem>> GetRecentAsync(Guid organizationId, int count, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(t => t.OrganizationId == organizationId).OrderByDescending(t => t.CreatedAt).Take(count).ToList());

    public Task<List<TaskItem>> GetOverdueAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<TaskItem>());
}
