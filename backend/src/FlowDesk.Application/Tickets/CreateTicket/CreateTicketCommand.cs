using FlowDesk.Domain.Tickets;

namespace FlowDesk.Application.Tickets.CreateTicket;

public sealed record CreateTicketCommand(
    Guid CustomerId,
    string Subject,
    string Description,
    TicketPriority Priority,
    Guid? AssignedUserId);
