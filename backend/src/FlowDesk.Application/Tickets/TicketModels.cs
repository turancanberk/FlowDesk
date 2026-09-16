using FlowDesk.Domain.Tickets;

namespace FlowDesk.Application.Tickets;

/// <param name="Number">Rendered as <c>TLP-1042</c> by the client.</param>
public sealed record TicketListItem(
    Guid Id,
    int Number,
    string Subject,
    Guid CustomerId,
    string CustomerName,
    TicketStatus Status,
    TicketPriority Priority,
    Guid? AssignedUserId,
    string? AssignedUserDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <param name="AvailableTransitions">
/// The statuses this ticket may move to right now. Sent with the ticket so the
/// interface can offer only the transitions the domain would accept, instead of
/// showing every status and letting the server reject four out of five.
/// </param>
/// <param name="Version">
/// The row version the client read. Sent back on update so a concurrent edit is
/// detected rather than silently overwritten (ADR-0013).
/// </param>
public sealed record TicketDetail(
    Guid Id,
    int Number,
    string Subject,
    string Description,
    Guid CustomerId,
    string CustomerName,
    TicketStatus Status,
    TicketPriority Priority,
    Guid? AssignedUserId,
    string? AssignedUserDisplayName,
    Guid CreatedByUserId,
    string? CreatedByDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyCollection<TicketStatus> AvailableTransitions,
    uint Version);

public sealed record TicketCommentItem(
    Guid Id,
    Guid AuthorUserId,
    string AuthorDisplayName,
    string Body,
    DateTimeOffset CreatedAt);

/// <summary>Sort orders a client may ask for.</summary>
public enum TicketSort
{
    RecentlyUpdated = 0,
    RecentlyCreated = 1,
    /// <summary>Most urgent first, then most recently updated.</summary>
    PriorityDescending = 2,
    NumberDescending = 3,
}
