namespace MyApp.Domain.Constants;

/// <summary>
/// Status of a task item in its lifecycle.
/// </summary>
public enum TaskItemStatus
{
    /// <summary>Task is created but not yet started.</summary>
    Todo = 0,

    /// <summary>Task is actively being worked on.</summary>
    InProgress = 1,

    /// <summary>Task has been completed successfully.</summary>
    Done = 2,

    /// <summary>Task was cancelled and will not be completed.</summary>
    Cancelled = 3
}
