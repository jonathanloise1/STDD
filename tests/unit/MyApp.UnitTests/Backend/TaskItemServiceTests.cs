using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using MyApp.Application.DTOs.Tasks;
using MyApp.Application.Exceptions;
using MyApp.Application.Logging;
using MyApp.Application.Services;
using MyApp.Domain.Constants;
using MyApp.Domain.Entities;
using NUnit.Framework;

namespace MyApp.UnitTests.Backend;

[TestFixture]
[Property("UserStory", "US-TASK-01")]
[Property("UserStory", "US-TASK-02")]
[Property("UserStory", "US-TASK-03")]
public class TaskItemServiceTests
{
    private CapturingLogger<TaskItemService> _log = null!;
    private FakeTaskItemsRepository _tasks = null!;
    private Organization _org = null!;
    private Dictionary<string, OrganizationUser> _members = null!;
    private TaskItemService _service = null!;

    [SetUp]
    public void SetUp()
    {
        (_org, _members) = FakeOrganizationsRepository.Build(
            ("admin", OrganizationRole.Admin), ("editor", OrganizationRole.Editor), ("viewer", OrganizationRole.Viewer));
        _log = new CapturingLogger<TaskItemService>();
        _tasks = new FakeTaskItemsRepository();
        _service = new TaskItemService(_log, _tasks, new FakeOrganizationsRepository(_org));
    }

    private Guid Aad(string member) => _members[member].User!.AadId;

    private TaskItem AddTask() 
    {
        var task = new TaskItem
        {
            Id = Guid.NewGuid(), OrganizationId = _org.Id, Title = "Order business cards",
            Description = "Ask the printer for a quote", DueDate = new DateOnly(2030, 3, 10),
            AssignedToUserId = Guid.NewGuid(), Status = TaskItemStatus.Todo
        };
        _tasks.Items.Add(task);
        return task;
    }

    [Test]
    public void Create_ByViewer_IsRefusedWithTheKeyAndAWarning()
    {
        var ex = Assert.ThrowsAsync<AccessLostException>(() =>
            _service.CreateAsync(Aad("viewer"), _org.Id, new CreateTaskRequest { Title = "x" }));

        Assert.That(ex!.Key, Is.EqualTo(TasksLog.Keys.TaskCreateNotAllowed));
        Assert.That(ex.Detail, Is.EqualTo("Your role does not allow this change."));
        Assert.That(_log.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(_tasks.Items, Is.Empty);
    }

    [Test]
    public void Update_ByViewer_IsRefusedWithTheKeyAndNothingChanges()
    {
        var task = AddTask();

        var ex = Assert.ThrowsAsync<AccessLostException>(() =>
            _service.UpdateAsync(Aad("viewer"), _org.Id, task.Id, new UpdateTaskRequest { Title = "Changed" }));

        Assert.That(ex!.Key, Is.EqualTo(TasksLog.Keys.TaskEditNotAllowed));
        Assert.That(_log.Entries.Single().LogKey, Is.EqualTo(TasksLog.Keys.TaskEditNotAllowed));
        Assert.That(task.Title, Is.EqualTo("Order business cards"));
    }

    [Test]
    public void Delete_ByViewer_IsRefusedAndTheTaskStays()
    {
        var task = AddTask();

        Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DeleteAsync(Aad("viewer"), _org.Id, task.Id));

        Assert.That(_tasks.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ChangeStatus_KeepsDescriptionDueDateAndAssignee()
    {
        var task = AddTask();
        var assignee = task.AssignedToUserId;

        var result = await _service.UpdateAsync(Aad("editor"), _org.Id, task.Id,
            new UpdateTaskRequest { Status = TaskItemStatus.InProgress });

        Assert.That(result.Status, Is.EqualTo("InProgress"));
        Assert.That(task.Title, Is.EqualTo("Order business cards"));
        Assert.That(task.Description, Is.EqualTo("Ask the printer for a quote"));
        Assert.That(task.DueDate, Is.EqualTo(new DateOnly(2030, 3, 10)));
        Assert.That(task.AssignedToUserId, Is.EqualTo(assignee));
    }

    [Test]
    public async Task Edit_OfTheTitleAndDueDate_KeepsTheRest()
    {
        var task = AddTask();
        var assignee = task.AssignedToUserId;

        await _service.UpdateAsync(Aad("admin"), _org.Id, task.Id,
            new UpdateTaskRequest { Title = "  Order cards  ", DueDate = new DateOnly(2030, 4, 1) });

        Assert.That(task.Title, Is.EqualTo("Order cards"));
        Assert.That(task.DueDate, Is.EqualTo(new DateOnly(2030, 4, 1)));
        Assert.That(task.Description, Is.EqualTo("Ask the printer for a quote"));
        Assert.That(task.AssignedToUserId, Is.EqualTo(assignee));
        Assert.That(task.Status, Is.EqualTo(TaskItemStatus.Todo));
    }

    [Test]
    public async Task Edit_CanClearTheDueDateAndTheDescriptionOnPurpose()
    {
        var task = AddTask();

        await _service.UpdateAsync(Aad("editor"), _org.Id, task.Id,
            new UpdateTaskRequest { Description = "", ClearDueDate = true });

        Assert.That(task.Description, Is.Null);
        Assert.That(task.DueDate, Is.Null);
    }

    [Test]
    public void Edit_WithABlankTitle_IsRefusedWithTheFieldMessage()
    {
        var task = AddTask();

        var ex = Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateAsync(Aad("editor"), _org.Id, task.Id, new UpdateTaskRequest { Title = "   " }));

        Assert.That(ex!.ValidationResult.ErrorMessage, Is.EqualTo("Enter a title."));
        Assert.That(ex.ValidationResult.MemberNames, Is.EqualTo(new[] { "title" }));
    }

    [Test]
    public void Update_OfADeletedTask_UsesTheEditKey_ButTheStatusKeyForTheStatusAlone()
    {
        var edit = Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _service.UpdateAsync(Aad("editor"), _org.Id, Guid.NewGuid(), new UpdateTaskRequest { Title = "x" }));
        var status = Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _service.UpdateAsync(Aad("editor"), _org.Id, Guid.NewGuid(), new UpdateTaskRequest { Status = TaskItemStatus.Done }));

        Assert.That(edit!.Key, Is.EqualTo(TasksLog.Keys.TaskEditNotFound));
        Assert.That(status!.Key, Is.EqualTo(TasksLog.Keys.TaskStatusNotFound));
        Assert.That(status.Detail, Is.EqualTo("This task no longer exists."));
        Assert.That(_log.Entries.Select(e => e.LogKey), Is.EqualTo(new[] { TasksLog.Keys.TaskEditNotFound, TasksLog.Keys.TaskStatusNotFound }));
    }

    [Test]
    public void Delete_OfADeletedTask_UsesTheDeleteKey()
    {
        var ex = Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _service.DeleteAsync(Aad("editor"), _org.Id, Guid.NewGuid()));

        Assert.That(ex!.Key, Is.EqualTo(TasksLog.Keys.TaskDeleteNotFound));
        Assert.That(_log.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
    }

    [Test]
    public async Task Dashboard_CountsEveryTask_AndListsTheFiveNewest()
    {
        for (var i = 0; i < 7; i++)
            _tasks.Items.Add(new TaskItem
            {
                Id = Guid.NewGuid(), OrganizationId = _org.Id, Title = $"T{i}",
                Status = i == 6 ? TaskItemStatus.Cancelled : TaskItemStatus.Todo,
                CreatedAt = new DateTime(2030, 1, 1 + i, 0, 0, 0, DateTimeKind.Utc)
            });
        var dashboard = new DashboardService(_tasks, new FakeOrganizationsRepository(_org));

        var counts = await dashboard.GetCountsAsync(Aad("viewer"), _org.Id);
        var recent = await dashboard.GetRecentTasksAsync(Aad("viewer"), _org.Id);

        Assert.That(counts.Total, Is.EqualTo(7));
        Assert.That(counts.Todo, Is.EqualTo(6));
        Assert.That(counts.Cancelled, Is.EqualTo(1));
        Assert.That(recent.Select(t => t.Title), Is.EqualTo(new[] { "T6", "T5", "T4", "T3", "T2" }));
    }
}
