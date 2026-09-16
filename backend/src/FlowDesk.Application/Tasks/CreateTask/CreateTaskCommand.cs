namespace FlowDesk.Application.Tasks.CreateTask;

/// <param name="DueAt">
/// Optional. Forcing a date on every task makes people invent one, and an
/// invented deadline makes the overdue list untrustworthy.
/// </param>
public sealed record CreateTaskCommand(
    string Title,
    string? Description,
    Guid? CustomerId,
    Guid? AssignedUserId,
    DateTimeOffset? DueAt);
