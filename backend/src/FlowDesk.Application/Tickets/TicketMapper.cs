using FlowDesk.Domain.Tickets;

namespace FlowDesk.Application.Tickets;

internal static class TicketMapper
{
    /// <param name="customerName">
    /// Falls back to a placeholder rather than an empty string: a detail page
    /// with a blank customer looks broken, where "Bilinmeyen müşteri" at least
    /// says what happened.
    /// </param>
    public static TicketDetail ToDetail(
        Ticket ticket,
        string? customerName,
        string? assignedUserDisplayName,
        string? createdByDisplayName) =>
        new(
            ticket.Id,
            ticket.Number,
            ticket.Subject,
            ticket.Description,
            ticket.CustomerId,
            customerName ?? "Bilinmeyen müşteri",
            ticket.Status,
            ticket.Priority,
            ticket.AssignedUserId,
            ticket.AssignedUserId is null ? null : assignedUserDisplayName ?? "Bilinmeyen kullanıcı",
            ticket.CreatedByUserId,
            createdByDisplayName,
            ticket.CreatedAt,
            ticket.UpdatedAt,
            ticket.ResolvedAt,
            ticket.AvailableTransitions,
            ticket.Version);
}
