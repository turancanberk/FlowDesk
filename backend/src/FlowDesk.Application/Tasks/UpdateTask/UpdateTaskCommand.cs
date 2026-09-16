namespace FlowDesk.Application.Tasks.UpdateTask;

/// <summary>
/// Replaces every editable field of a task.
/// </summary>
/// <remarks>
/// All fields are always sent, so null means "none" rather than "leave alone".
/// That removes the ambiguity a partial body would carry — in JSON an absent
/// field and a null field arrive the same way — and a task's edit form holds
/// all of these fields anyway.
/// </remarks>
public sealed record UpdateTaskCommand(
    string Title,
    string? Description,
    Guid? CustomerId,
    Guid? AssignedUserId,
    DateTimeOffset? DueAt);
