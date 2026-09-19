using FlowDesk.Application.Abstractions;

namespace FlowDesk.Application.Tickets;

/*
  What travels in a message, and what does not.

  Identifiers and the facts of the event travel: which ticket, which number,
  what it was called at the time. Those describe what happened, and a
  notification is a record of a moment rather than a live view — a ticket that
  has since been renamed should not silently rewrite the notice that went out.

  Display names do not travel. Resolving them costs a query, and doing it in the
  request that assigns a ticket would put that cost on a person waiting for a
  page. The consumer runs in the background where the same query costs nothing,
  and it also means a renamed person appears under their current name.
*/

/// <summary>A ticket was handed to someone.</summary>
/// <param name="AssignedByUserId">
/// Who gave it. Carried so a person assigning a ticket to themselves is not
/// notified about their own action.
/// </param>
public sealed record TicketAssigned(
    Guid MessageId,
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid TicketId,
    int TicketNumber,
    string Subject,
    Guid AssignedUserId,
    Guid AssignedByUserId,
    string WorkspaceSlug) : IntegrationMessage(MessageId, TenantId, OccurredAt)
{
    public const string Key = "ticket.assigned";

    public override string RoutingKey => Key;
}

/// <summary>Someone wrote on a ticket.</summary>
/// <param name="AuthorUserId">
/// Carried so the author is not notified about their own comment.
/// </param>
/// <param name="Excerpt">
/// The opening of the comment, for the notification line. Stored rather than
/// read back, because the comment may be gone by the time anyone looks.
/// </param>
/// <param name="AssigneeUserId">
/// Who the ticket belonged to when the comment was written, or null if nobody.
/// Carried rather than looked up on delivery: the outbox delivers seconds
/// later, or minutes after a broker outage, and by then the ticket may have
/// changed hands — the notice would go to someone the comment was never meant
/// for, and miss the person it was (found in Phase 17).
/// </param>
public sealed record TicketCommented(
    Guid MessageId,
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid TicketId,
    int TicketNumber,
    string Subject,
    Guid AuthorUserId,
    Guid? AssigneeUserId,
    string Excerpt,
    string WorkspaceSlug) : IntegrationMessage(MessageId, TenantId, OccurredAt)
{
    public const string Key = "ticket.commented";

    public override string RoutingKey => Key;
}
