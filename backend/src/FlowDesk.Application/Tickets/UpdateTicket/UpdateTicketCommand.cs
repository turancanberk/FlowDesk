using FlowDesk.Domain.Tickets;

namespace FlowDesk.Application.Tickets.UpdateTicket;

/// <param name="Version">
/// The row version the editor loaded. Required, because this command replaces
/// free text: without it, two people editing the same description would lose
/// one version with no trace (ADR-0013).
/// </param>
public sealed record UpdateTicketCommand(
    string Subject,
    string Description,
    TicketPriority Priority,
    Guid CustomerId,
    uint Version);
