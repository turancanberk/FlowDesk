namespace FlowDesk.Infrastructure.Notifications;

/*
  What a notification stores so it can be rendered later.

  Written at the moment the event happened rather than joined at read time. A
  notification is a record of a moment: it should still read correctly after the
  ticket has been renamed, reassigned, or deleted outright.
*/

/// <param name="WorkspaceSlug">Needed to build the link; the row itself only knows the tenant id.</param>
public sealed record TicketAssignedPayload(
    Guid TicketId,
    int TicketNumber,
    string Subject,
    string AssignedByDisplayName,
    string WorkspaceSlug);

public sealed record TicketCommentedPayload(
    Guid TicketId,
    int TicketNumber,
    string Subject,
    string AuthorDisplayName,
    string Excerpt,
    string WorkspaceSlug);
