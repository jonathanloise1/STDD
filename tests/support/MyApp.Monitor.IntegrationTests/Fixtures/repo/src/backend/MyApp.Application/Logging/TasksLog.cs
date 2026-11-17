using Microsoft.Extensions.Logging;

namespace MyApp.Application.Logging;

public static partial class TasksLog
{
    public static class Keys
    {
        public const string TaskCreateAccessLost = "task.create.access_lost";
        public const string TaskCreateUnavailable = "task.create.unavailable";
    }

    // US-TASK-01
    [LoggerMessage(EventId = 1, EventName = Keys.TaskCreateAccessLost, Level = LogLevel.Warning,
        Message = "Task not created: user {UserId} has no access to organization {OrganizationId}")]
    public static partial void TaskCreateAccessLost(this ILogger logger, Guid userId, Guid organizationId);

    // US-TASK-01
    [LoggerMessage(EventId = 2, EventName = Keys.TaskCreateUnavailable, Level = LogLevel.Error,
        Message = "Task not created: the service failed for organization {OrganizationId}")]
    public static partial void TaskCreateUnavailable(this ILogger logger, Exception exception, Guid organizationId);
}
