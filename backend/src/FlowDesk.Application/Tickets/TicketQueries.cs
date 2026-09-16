using FlowDesk.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tickets;

/// <summary>
/// Shared lookups for turning ticket rows into display models.
/// </summary>
/// <remarks>
/// Customer names live in our own tables but user display names live in
/// Identity, so a ticket list needs both. Resolving them in one batch each
/// keeps a page of twenty-five tickets to three queries instead of fifty.
/// </remarks>
internal static class TicketQueries
{
    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveCustomerNamesAsync(
        IFlowDeskDbContext dbContext,
        IReadOnlyCollection<Guid> customerIds,
        CancellationToken cancellationToken)
    {
        if (customerIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        // IgnoreQueryFilters: a ticket's customer may have been archived, and
        // the ticket still has to show whose request it is. The workspace scope
        // is preserved because the ids come from tickets already scoped to it.
        return await dbContext.Customers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(customer => customerIds.Contains(customer.Id))
            .Select(customer => new { customer.Id, customer.Name })
            .ToDictionaryAsync(row => row.Id, row => row.Name, cancellationToken);
    }

    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveUserNamesAsync(
        IUserAccountStore accountStore,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var accounts = await accountStore.FindByIdsAsync(userIds, cancellationToken);

        return accounts.ToDictionary(entry => entry.Key, entry => entry.Value.DisplayName);
    }
}
