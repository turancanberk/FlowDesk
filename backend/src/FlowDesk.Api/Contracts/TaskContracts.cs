using FlowDesk.Domain.Tasks;

namespace FlowDesk.Api.Contracts;

/// <param name="IsOverdue">
/// Computed server-side against the request time, so every client agrees on
/// what is late without each one re-deriving it from a timezone-shifted clock.
/// </param>
public sealed record TaskListItemResponse(
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

public sealed record TaskDetailResponse(
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
