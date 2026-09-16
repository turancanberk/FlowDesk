using FlowDesk.Domain.Tickets;

namespace FlowDesk.Application.Tickets.ListTickets;

/// <param name="Search">Matches the subject, or a ticket number typed with or without the prefix.</param>
/// <param name="AssignedUserId">Backs the "my tickets" view.</param>
/// <param name="Unassigned">
/// Distinct from a null <paramref name="AssignedUserId"/>, which means "any
/// assignee". Finding the tickets nobody has picked up is its own question.
/// </param>
public sealed record ListTicketsQuery(
    string? Search,
    TicketStatus? Status,
    TicketPriority? Priority,
    Guid? CustomerId,
    Guid? AssignedUserId,
    bool Unassigned,
    TicketSort Sort,
    int? Page,
    int? PageSize);
