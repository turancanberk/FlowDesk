using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tickets;

/// <summary>
/// The steps every ticket write shares: load the ticket, build its display
/// model, and save without losing a concurrent edit.
/// </summary>
/// <remarks>
/// Five handlers change a ticket and all five have to return the same shaped
/// detail and handle the same conflict. Writing that once is what keeps the
/// fifth one from quietly skipping the conflict check.
/// </remarks>
internal static class TicketWorkflow
{
    /// <summary>
    /// Loads a tracked ticket in the current workspace, or null.
    /// </summary>
    /// <remarks>
    /// No workspace condition is written here: the global query filter already
    /// scopes the set, so a ticket belonging to another workspace is simply not
    /// found (ADR-0024). That is also why the API can answer 404 rather than
    /// 403 without leaking that the ticket exists (ADR-0007).
    /// </remarks>
    public static Task<Ticket?> FindAsync(
        IFlowDeskDbContext dbContext,
        Guid ticketId,
        CancellationToken cancellationToken) =>
        dbContext.Tickets.FirstOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);

    /// <summary>
    /// Saves, turning a lost-update collision into a conflict result.
    /// </summary>
    /// <remarks>
    /// PostgreSQL's xmin is the concurrency token (ADR-0013), so the UPDATE
    /// carries the row version we read. If someone else has written since, it
    /// matches no rows and EF raises this exception; reporting it is how the
    /// caller learns their change did not land.
    /// </remarks>
    public static async Task<Result> SaveAsync(
        IFlowDeskDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(TicketErrors.ConcurrencyConflict);
        }
    }

    /// <summary>
    /// Builds the detail model, resolving the customer and people involved.
    /// </summary>
    public static async Task<TicketDetail> ToDetailAsync(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        Ticket ticket,
        CancellationToken cancellationToken)
    {
        var customerNames = await TicketQueries.ResolveCustomerNamesAsync(
            dbContext, [ticket.CustomerId], cancellationToken);

        var userIds = ticket.AssignedUserId is { } assignedUserId
            ? new[] { ticket.CreatedByUserId, assignedUserId }.Distinct().ToArray()
            : [ticket.CreatedByUserId];

        var userNames = await TicketQueries.ResolveUserNamesAsync(
            accountStore, userIds, cancellationToken);

        return TicketMapper.ToDetail(
            ticket,
            customerNames.GetValueOrDefault(ticket.CustomerId),
            ticket.AssignedUserId is null
                ? null
                : userNames.GetValueOrDefault(ticket.AssignedUserId.Value),
            userNames.GetValueOrDefault(ticket.CreatedByUserId));
    }
}
