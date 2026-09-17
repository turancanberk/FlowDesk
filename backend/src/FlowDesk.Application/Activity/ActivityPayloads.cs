using FlowDesk.Domain.Tasks;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;

namespace FlowDesk.Application.Activity;

/*
  What each kind of event stores so its line can be written later.

  Every payload carries a name or a number the reader recognises, and nothing
  else. No password, no token, no invitation link, no comment body, no file
  content. An audit trail is read by more people than the thing it describes,
  and for longer (docs/SECURITY.md).

  Written at the moment rather than joined at read time, so a line still reads
  correctly after the record it names has been renamed or deleted — which for a
  deletion event is the only way it can read at all.
*/

public sealed record CustomerActivityPayload(string Name);

/// <param name="Number">Rendered as TLP-1042 by the client.</param>
public sealed record TicketActivityPayload(int Number, string Subject);

/// <param name="From">Null when the ticket was just created.</param>
public sealed record TicketStatusActivityPayload(
    int Number,
    string Subject,
    TicketStatus? From,
    TicketStatus To);

/// <param name="AssignedUserId">
/// The id only. The name is resolved when the line is read, so a renamed
/// colleague appears under their current name.
/// </param>
public sealed record TicketAssignmentActivityPayload(
    int Number,
    string Subject,
    Guid? AssignedUserId);

public sealed record TaskActivityPayload(string Title, TaskItemStatus Status);

/// <param name="Email">
/// The address invited. Not sensitive in this context — it was typed by a
/// teammate and is already visible on the invitations list — and without it the
/// line cannot say who was invited.
/// </param>
public sealed record MemberInvitationActivityPayload(string Email, MembershipRole Role);

public sealed record MemberActivityPayload(
    Guid MemberUserId,
    MembershipRole? FromRole,
    MembershipRole ToRole);

/// <param name="FileName">
/// Already cleaned when the attachment was stored, so what goes in here cannot
/// carry control characters into a line of history (ADR-0035).
/// </param>
public sealed record AttachmentActivityPayload(string FileName, Guid TicketId, int TicketNumber);
