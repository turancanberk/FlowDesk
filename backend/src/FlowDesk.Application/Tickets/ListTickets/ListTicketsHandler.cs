using System.Globalization;
using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tickets.ListTickets;

public sealed class ListTicketsHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;

    public ListTicketsHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<PagedResult<TicketListItem>>> HandleAsync(
        ListTicketsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ViewTickets))
        {
            return Result.Failure<PagedResult<TicketListItem>>(
                TenancyErrors.InsufficientRole("Talepleri görüntülemek"));
        }

        var page = new PageRequest(query.Page, query.PageSize);
        var tickets = BuildQuery(query);

        var totalCount = await tickets.CountAsync(cancellationToken);

        var rows = await ApplySort(tickets, query.Sort)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(ticket => new
            {
                ticket.Id,
                ticket.Number,
                ticket.Subject,
                ticket.CustomerId,
                ticket.Status,
                ticket.Priority,
                ticket.AssignedUserId,
                ticket.CreatedAt,
                ticket.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        var customerNames = await TicketQueries.ResolveCustomerNamesAsync(
            _dbContext,
            rows.Select(row => row.CustomerId).Distinct().ToArray(),
            cancellationToken);

        var userNames = await TicketQueries.ResolveUserNamesAsync(
            _accountStore,
            rows.Where(row => row.AssignedUserId is not null)
                .Select(row => row.AssignedUserId!.Value)
                .Distinct()
                .ToArray(),
            cancellationToken);

        var items = rows
            .Select(row => new TicketListItem(
                row.Id,
                row.Number,
                row.Subject,
                row.CustomerId,
                // A customer that vanished mid-request would otherwise render as
                // a blank cell with no explanation.
                customerNames.GetValueOrDefault(row.CustomerId, "Bilinmeyen müşteri"),
                row.Status,
                row.Priority,
                row.AssignedUserId,
                row.AssignedUserId is null
                    ? null
                    : userNames.GetValueOrDefault(row.AssignedUserId.Value, "Bilinmeyen kullanıcı"),
                row.CreatedAt,
                row.UpdatedAt))
            .ToList();

        return Result.Success(new PagedResult<TicketListItem>(
            items,
            page.Page,
            page.PageSize,
            totalCount));
    }

    private IQueryable<Ticket> BuildQuery(ListTicketsQuery query)
    {
        var tickets = _dbContext.Tickets.AsNoTracking();

        if (query.Status is { } status)
        {
            tickets = tickets.Where(ticket => ticket.Status == status);
        }

        if (query.Priority is { } priority)
        {
            tickets = tickets.Where(ticket => ticket.Priority == priority);
        }

        if (query.CustomerId is { } customerId)
        {
            tickets = tickets.Where(ticket => ticket.CustomerId == customerId);
        }

        if (query.Unassigned)
        {
            tickets = tickets.Where(ticket => ticket.AssignedUserId == null);
        }
        else if (query.AssignedUserId is { } assignedUserId)
        {
            tickets = tickets.Where(ticket => ticket.AssignedUserId == assignedUserId);
        }

        var search = query.Search?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            /*
              Teams quote tickets by number far more often than by subject, so a
              search that looks like one is treated as one. "TLP-1042", "1042"
              and "tlp-1042" all find the same ticket.
            */
            if (TryParseTicketNumber(search, out var number))
            {
                tickets = tickets.Where(ticket => ticket.Number == number);
            }
            else
            {
                var pattern = $"%{search.ToLowerInvariant()}%";

                // Subjects are free text typed by the team; the same Turkish
                // folding as customers would need its own stored column, which
                // is not justified until subject search is shown to be slow
                // (ADR-0025).
#pragma warning disable CA1304, CA1311
                tickets = tickets.Where(ticket => EF.Functions.Like(ticket.Subject.ToLower(), pattern));
#pragma warning restore CA1304, CA1311
            }
        }

        return tickets;
    }

    private static bool TryParseTicketNumber(string search, out int number)
    {
        var candidate = search;

        if (candidate.StartsWith($"{TicketNumber.Prefix}-", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[(TicketNumber.Prefix.Length + 1)..];
        }

        return int.TryParse(candidate, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    /// <summary>
    /// Applies the requested order, always with <c>Number</c> as a tiebreaker.
    /// </summary>
    /// <remarks>
    /// Number is unique within a workspace, so it gives every sort a stable
    /// total order. Without it, rows sharing a timestamp or a priority could
    /// appear twice or be skipped while paging
    /// (docs/API_CONVENTIONS.md).
    /// </remarks>
    private static IQueryable<Ticket> ApplySort(IQueryable<Ticket> tickets, TicketSort sort) =>
        sort switch
        {
            TicketSort.RecentlyCreated =>
                tickets.OrderByDescending(ticket => ticket.CreatedAt).ThenByDescending(ticket => ticket.Number),
            TicketSort.PriorityDescending =>
                tickets.OrderByDescending(ticket => ticket.Priority)
                    .ThenByDescending(ticket => ticket.UpdatedAt)
                    .ThenByDescending(ticket => ticket.Number),
            TicketSort.NumberDescending =>
                tickets.OrderByDescending(ticket => ticket.Number),
            _ =>
                tickets.OrderByDescending(ticket => ticket.UpdatedAt).ThenByDescending(ticket => ticket.Number),
        };
}
