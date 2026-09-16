namespace FlowDesk.Application.Tickets.AssignTicket;

/// <param name="AssignedUserId">
/// Null hands the ticket back to the unassigned queue, which is a deliberate
/// action rather than a missing value.
/// </param>
public sealed record AssignTicketCommand(Guid? AssignedUserId);
