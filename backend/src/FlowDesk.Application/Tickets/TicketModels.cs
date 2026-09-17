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

/// <param name="UploadedByUserId">
/// Resolved to a name by the client from the team list it already has, rather
/// than joined here: an attachment list is short and the lookup would add a
/// query for a line of text.
/// </param>
public sealed record AttachmentItem(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeInBytes,
    Guid UploadedByUserId,
    DateTimeOffset CreatedAt);

/// <summary>An attachment's content, ready to be streamed back.</summary>
/// <remarks>
/// The caller disposes the stream. It is opened rather than buffered so a large
/// file does not have to fit in memory twice on its way out.
/// </remarks>
public sealed record AttachmentDownload(
    Stream Content,
    string FileName,
    string ContentType);

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
