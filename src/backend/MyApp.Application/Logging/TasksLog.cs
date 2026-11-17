using Microsoft.Extensions.Logging;
using MyApp.Domain.Entities;

namespace MyApp.Application.Logging;

/// <summary>
/// Log definitions of the Tasks area (operations.md 3.1). One class per area in this folder:
/// the key constant in <see cref="Keys"/> is the only string literal of the key under src/backend/.
/// </summary>
public static partial class TasksLog
{
    public static class Keys
    {
        public const string TaskCreateNotAllowed = "task.create.not_allowed";
        public const string TaskCreateUnavailable = "task.create.unavailable";
        public const string TaskListUnavailable = "task.list.unavailable";
        public const string TaskEditNotAllowed = "task.edit.not_allowed";
        public const string TaskEditNotFound = "task.edit.not_found";
        public const string TaskStatusNotFound = "task.status.not_found";
        public const string TaskDeleteNotFound = "task.delete.not_found";
    }

    // US-TASK-01
    [LoggerMessage(EventId = 1, EventName = Keys.TaskCreateNotAllowed, Level = LogLevel.Warning,
        Message = "Task not created: the role {Role} of user {UserId} in organization {OrganizationId} does not allow it")]
    public static partial void TaskCreateNotAllowed(this ILogger logger, OrganizationRole role, Guid userId, Guid organizationId);

    // US-TASK-01
    [LoggerMessage(EventId = 2, EventName = Keys.TaskCreateUnavailable, Level = LogLevel.Error,
        Message = "Task not created: the service did not answer ({ExceptionType} at {TopFrame})")]
    public static partial void TaskCreateUnavailable(this ILogger logger, Exception exception, string exceptionType, string topFrame);

    // US-TASK-05
    [LoggerMessage(EventId = 3, EventName = Keys.TaskListUnavailable, Level = LogLevel.Error,
        Message = "Tasks not listed: the service did not answer ({ExceptionType} at {TopFrame})")]
    public static partial void TaskListUnavailable(this ILogger logger, Exception exception, string exceptionType, string topFrame);

    // US-TASK-02
    [LoggerMessage(EventId = 4, EventName = Keys.TaskEditNotAllowed, Level = LogLevel.Warning,
        Message = "Task not updated: the role {Role} of user {UserId} in organization {OrganizationId} does not allow it")]
    public static partial void TaskEditNotAllowed(this ILogger logger, OrganizationRole role, Guid userId, Guid organizationId);

    // US-TASK-02
    [LoggerMessage(EventId = 5, EventName = Keys.TaskEditNotFound, Level = LogLevel.Warning,
        Message = "Task not updated: task {TaskId} does not exist in organization {OrganizationId}")]
    public static partial void TaskEditNotFound(this ILogger logger, Guid taskId, Guid organizationId);

    // US-TASK-03
    [LoggerMessage(EventId = 6, EventName = Keys.TaskStatusNotFound, Level = LogLevel.Warning,
        Message = "Task status not changed: task {TaskId} does not exist in organization {OrganizationId}")]
    public static partial void TaskStatusNotFound(this ILogger logger, Guid taskId, Guid organizationId);

    // US-TASK-04
    [LoggerMessage(EventId = 7, EventName = Keys.TaskDeleteNotFound, Level = LogLevel.Warning,
        Message = "Task not deleted: task {TaskId} does not exist in organization {OrganizationId}")]
    public static partial void TaskDeleteNotFound(this ILogger logger, Guid taskId, Guid organizationId);
}
