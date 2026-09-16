using FlowDesk.Domain.Tasks;

namespace FlowDesk.Application.Tasks.ListTasks;

/// <param name="Search">Matches part of the title.</param>
/// <param name="Unassigned">
/// Distinct from a null <paramref name="AssignedUserId"/>, which means "any
/// assignee". Finding the work nobody has picked up is its own question.
/// </param>
/// <param name="Overdue">
/// Past its due date and not done. The one filter a task list exists for.
/// </param>
public sealed record ListTasksQuery(
    string? Search,
    TaskItemStatus? Status,
    Guid? CustomerId,
    Guid? AssignedUserId,
    bool Unassigned,
    bool Overdue,
    TaskSort Sort,
    int? Page,
    int? PageSize);
