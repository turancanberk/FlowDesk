using FlowDesk.Domain.Tickets;

namespace FlowDesk.Api.Contracts;

/// <param name="Number">
/// The raw sequence number. Rendered as <c>TLP-1042</c> by the client, which
/// owns how the identifier is shown.
/// </param>
public sealed record TicketListItemResponse(
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
/// The statuses this ticket may move to right now, so the interface can offer
/// only those instead of listing all five and letting the server refuse four.
/// </param>
/// <param name="Version">
/// Row version. Sent back with <c>PATCH</c> so a concurrent edit is reported
/// rather than silently overwritten.
/// </param>
public sealed record TicketDetailResponse(
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

public sealed record TicketCommentResponse(
    Guid Id,
    Guid AuthorUserId,
    string AuthorDisplayName,
    string Body,
    DateTimeOffset CreatedAt);
