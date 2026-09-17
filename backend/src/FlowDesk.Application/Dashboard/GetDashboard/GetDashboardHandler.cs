using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Tasks;
using FlowDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Dashboard.GetDashboard;

/// <summary>
/// Reads the operational figures for a workspace.
/// </summary>
/// <remarks>
/// Cached, briefly and per workspace. The page runs eight queries and is opened
/// far more often than the figures change, which is what makes it the one place
/// a cache earns its staleness (ADR-0015).
///
/// <para>
/// The cached copy carries the instant it was read, so the screen can say how
/// fresh it is rather than implying it is live. A figure at most a minute old,
/// labelled as such, is more honest than one that looks current and is not.
/// </para>
///
/// <para>
/// Every figure is read against one instant, taken once at the top. Reading the
/// clock per query would let a task be overdue in the count and not in the list
/// beside it.
/// </para>
/// </remarks>
public sealed class GetDashboardHandler
{
    /// <summary>How far ahead "due soon" looks.</summary>
    /// <remarks>
    /// A week, because that is the horizon a support team plans over. Longer and
    /// the number stops meaning "attend to this"; shorter and it misses the work
    /// someone should be starting now.
    /// </remarks>
    private static readonly TimeSpan DueSoonWindow = TimeSpan.FromDays(7);

    private const int RecentTicketLimit = 5;
    private const int UpcomingTaskLimit = 5;

    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ICache _cache;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly TimeSpan _cacheTtl;

    public GetDashboardHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ICache cache,
        IDashboardCachePolicy cachePolicy,
        ITenantContext tenantContext,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(cachePolicy);

        _dbContext = dbContext;
        _accountStore = accountStore;
        _cache = cache;
        _tenantContext = tenantContext;
        _clock = clock;
        _cacheTtl = cachePolicy.DashboardTtl;
    }

    public async Task<Result<DashboardSummary>> HandleAsync(CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.View))
        {
            return Result.Failure<DashboardSummary>(
                TenancyErrors.InsufficientRole("Çalışma alanını görüntülemek"));
        }

        var tenantId = _tenantContext.TenantId;

        /*
          The key carries the workspace, and that is the one thing that must
          never be wrong here: without it a cache would serve one organisation's
          figures to another. It is built in one place for exactly that reason
          (ADR-0037).
        */
        var cacheKey = CacheKeys.Dashboard(tenantId);

        var cached = await _cache.GetAsync<DashboardSummary>(cacheKey, cancellationToken);

        if (cached is not null)
        {
            return Result.Success(cached);
        }

        var now = _clock.UtcNow;
        var dueSoonBefore = now + DueSoonWindow;

        // Customers, tickets and tasks are all scoped by the global query
        // filter (ADR-0024).
        var customerCount = await _dbContext.Customers.AsNoTracking()
            .CountAsync(cancellationToken);

        /*
          Memberships are deliberately outside the query filter, because "which
          workspaces do I belong to?" has to look across all of them (ADR-0024).
          That makes this the one count on the page where the workspace
          condition has to be written by hand — and the one place a missing
          condition would silently report another organisation's headcount.
        */
        var memberCount = await _dbContext.Memberships.AsNoTracking()
            .CountAsync(membership => membership.TenantId == tenantId, cancellationToken);

        var ticketsByStatus = await _dbContext.Tickets.AsNoTracking()
            .GroupBy(ticket => ticket.Status)
            .Select(group => new TicketStatusCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        /*
          "Open" on a dashboard means unfinished, not the Open status alone. A
          ticket sitting in InProgress or Waiting is still someone's problem,
          and a number that excluded them would read as calm while the queue
          filled up.
        */
        var openTicketCount = ticketsByStatus
            .Where(entry => entry.Status is
                TicketStatus.Open or TicketStatus.InProgress or TicketStatus.Waiting)
            .Sum(entry => entry.Count);

        var unassignedTicketCount = await _dbContext.Tickets.AsNoTracking()
            .CountAsync(
                ticket => ticket.AssignedUserId == null
                    && ticket.Status != TicketStatus.Resolved
                    && ticket.Status != TicketStatus.Closed,
                cancellationToken);

        // Finished work is never overdue, however late it was done — the same
        // condition the task list uses (ADR-0030).
        var overdueTaskCount = await _dbContext.Tasks.AsNoTracking()
            .CountAsync(
                task => task.Status != TaskItemStatus.Done
                    && task.DueAt != null
                    && task.DueAt < now,
                cancellationToken);

        var dueSoonTaskCount = await _dbContext.Tasks.AsNoTracking()
            .CountAsync(
                task => task.Status != TaskItemStatus.Done
                    && task.DueAt != null
                    && task.DueAt >= now
                    && task.DueAt <= dueSoonBefore,
                cancellationToken);

        var recentTickets = await ReadRecentTicketsAsync(cancellationToken);
        var upcomingTasks = await ReadUpcomingTasksAsync(now, cancellationToken);

        var summary = new DashboardSummary(
            customerCount,
            openTicketCount,
            unassignedTicketCount,
            overdueTaskCount,
            dueSoonTaskCount,
            memberCount,
            // Ordered by the enum so the chart's bars keep their places as the
            // numbers move; ordering by count would make it jump around.
            [.. ticketsByStatus.OrderBy(entry => entry.Status)],
            recentTickets,
            upcomingTasks,
            now);

        /*
          Stored after the figures are read, not before. A failed write simply
          means the next request recomputes them, which is what would have
          happened anyway.
        */
        await _cache.SetAsync(cacheKey, summary, _cacheTtl, cancellationToken);

        return Result.Success(summary);
    }

    private async Task<IReadOnlyList<RecentTicket>> ReadRecentTicketsAsync(
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.Tickets.AsNoTracking()
            .OrderByDescending(ticket => ticket.UpdatedAt)
            .ThenByDescending(ticket => ticket.Number)
            .Take(RecentTicketLimit)
            .Select(ticket => new
            {
                ticket.Id,
                ticket.Number,
                ticket.Subject,
                ticket.CustomerId,
                ticket.Status,
                ticket.Priority,
                ticket.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        // IgnoreQueryFilters: an archived customer's tickets still belong on the
        // list, and the row has to say whose they are. The workspace scope is
        // preserved because the ids come from tickets already scoped to it.
        var customerNames = await _dbContext.Customers.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(customer => rows.Select(row => row.CustomerId).Contains(customer.Id))
            .Select(customer => new { customer.Id, customer.Name })
            .ToDictionaryAsync(entry => entry.Id, entry => entry.Name, cancellationToken);

        return [.. rows.Select(row => new RecentTicket(
            row.Id,
            row.Number,
            row.Subject,
            customerNames.GetValueOrDefault(row.CustomerId, "Bilinmeyen müşteri"),
            row.Status,
            row.Priority,
            row.UpdatedAt))];
    }

    private async Task<IReadOnlyList<UpcomingTask>> ReadUpcomingTasksAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        /*
          Overdue work first, then what is due next. Both belong here: a list
          that showed only future deadlines would quietly drop the things
          already late, which are the ones that need attention most.
        */
        var rows = await _dbContext.Tasks.AsNoTracking()
            .Where(task => task.Status != TaskItemStatus.Done && task.DueAt != null)
            .OrderBy(task => task.DueAt)
            .ThenBy(task => task.Id)
            .Take(UpcomingTaskLimit)
            .Select(task => new
            {
                task.Id,
                task.Title,
                task.DueAt,
                task.AssignedUserId,
            })
            .ToListAsync(cancellationToken);

        var accounts = await _accountStore.FindByIdsAsync(
            [.. rows.Where(row => row.AssignedUserId is not null)
                .Select(row => row.AssignedUserId!.Value)
                .Distinct()],
            cancellationToken);

        return [.. rows.Select(row => new UpcomingTask(
            row.Id,
            row.Title,
            // Never null: the query keeps only dated tasks.
            row.DueAt!.Value,
            row.DueAt.Value < now,
            row.AssignedUserId is not null && accounts.TryGetValue(row.AssignedUserId.Value, out var account)
                ? account.DisplayName
                : null))];
    }
}
