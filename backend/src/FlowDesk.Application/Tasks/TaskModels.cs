using FlowDesk.Domain.Tasks;

namespace FlowDesk.Application.Tasks;

/// <param name="IsOverdue">
/// Computed against the current time rather than stored, so a task does not
/// need a nightly job to become overdue.
/// </param>
public sealed record TaskListItem(
    Guid Id,
    string Title,
    TaskItemStatus Status,
    DateTimeOffset? DueAt,
    bool IsOverdue,
    Guid? AssignedUserId,
    string? AssignedUserDisplayName,
    Guid? CustomerId,
    string? CustomerName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TaskDetail(
    Guid Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    DateTimeOffset? DueAt,
    bool IsOverdue,
    Guid? AssignedUserId,
    string? AssignedUserDisplayName,
    Guid? CustomerId,
    string? CustomerName,
    Guid CreatedByUserId,
    string? CreatedByDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

/// <summary>Sort orders a client may ask for.</summary>
public enum TaskSort
{
    /// <summary>
    /// Soonest deadline first, with undated tasks after them.
    /// </summary>
    /// <remarks>
    /// The default, because the question a task list answers is "what is due
    /// next". An undated task has not been scheduled, so it belongs below the
    /// ones that have been — not at the top, which is where a naive ascending
    /// sort on a nullable column puts nulls in PostgreSQL's default ordering.
    /// </remarks>
    DueSoonest = 0,
    RecentlyCreated = 1,
    RecentlyUpdated = 2,
    TitleAscending = 3,
}
