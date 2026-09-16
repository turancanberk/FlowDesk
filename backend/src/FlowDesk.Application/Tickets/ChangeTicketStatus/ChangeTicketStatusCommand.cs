using FlowDesk.Domain.Tickets;

namespace FlowDesk.Application.Tickets.ChangeTicketStatus;

public sealed record ChangeTicketStatusCommand(TicketStatus Status);
