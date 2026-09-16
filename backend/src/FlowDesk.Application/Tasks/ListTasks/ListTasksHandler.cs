using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tasks.ListTasks;

public sealed class ListTasksHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public ListTasksHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ITenantContext tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Result<PagedResult<TaskListItem>>> HandleAsync(
        ListTasksQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ViewTasks))
        {
            return Result.Failure<PagedResult<TaskListItem>>(
                TenancyErrors.InsufficientRole("Görevleri görüntülemek"));
        }

        var now = _clock.UtcNow;
        var page = new PageRequest(query.Page, query.PageSize);
        var tasks = BuildQuery(query, now);

        var totalCount = await tasks.CountAsync(cancellationToken);

        var rows = await ApplySort(tasks, query.Sort)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(task => new
            {
                task.Id,
                task.Title,
                task.Status,
                task.DueAt,
                task.AssignedUserId,
                task.CustomerId,
                task.CreatedAt,
                task.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        var customerNames = await TaskQueries.ResolveCustomerNamesAsync(
            _dbContext,
            rows.Where(row => row.CustomerId is not null)
                .Select(row => row.CustomerId!.Value)
                .Distinct()
                .ToArray(),
            cancellationToken);

        var userNames = await TaskQueries.ResolveUserNamesAsync(
            _accountStore,
            rows.Where(row => row.AssignedUserId is not null)
                .Select(row => row.AssignedUserId!.Value)
                .Distinct()
                .ToArray(),
            cancellationToken);

        var items = rows
            .Select(row => new TaskListItem(
                row.Id,
                row.Title,
                row.Status,
                row.DueAt,
                row.Status is not TaskItemStatus.Done && row.DueAt is { } dueAt && dueAt < now,
                row.AssignedUserId,
                row.AssignedUserId is null
                    ? null
                    : userNames.GetValueOrDefault(row.AssignedUserId.Value, "Bilinmeyen kullanıcı"),
                row.CustomerId,
                row.CustomerId is null
                    ? null
                    : customerNames.GetValueOrDefault(row.CustomerId.Value, "Bilinmeyen müşteri"),
                row.CreatedAt,
                row.UpdatedAt))
            .ToList();

        return Result.Success(new PagedResult<TaskListItem>(
            items,
            page.Page,
            page.PageSize,
            totalCount));
    }

    private IQueryable<TaskItem> BuildQuery(ListTasksQuery query, DateTimeOffset now)
    {
        var tasks = _dbContext.Tasks.AsNoTracking();

        if (query.Status is { } status)
        {
            tasks = tasks.Where(task => task.Status == status);
        }

        if (query.CustomerId is { } customerId)
        {
            tasks = tasks.Where(task => task.CustomerId == customerId);
        }

        if (query.Unassigned)
        {
            tasks = tasks.Where(task => task.AssignedUserId == null);
        }
        else if (query.AssignedUserId is { } assignedUserId)
        {
            tasks = tasks.Where(task => task.AssignedUserId == assignedUserId);
        }

        if (query.Overdue)
        {
            // Finished work is never overdue, however late it was done.
            tasks = tasks.Where(task =>
                task.Status != TaskItemStatus.Done && task.DueAt != null && task.DueAt < now);
        }

        var search = query.Search?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{search.ToLowerInvariant()}%";

            // Titles are free text typed by the team. The Turkish folding used
            // for customer search needs its own stored column, which is not
            // justified until title search is shown to be slow (ADR-0025).
#pragma warning disable CA1304, CA1311
            tasks = tasks.Where(task => EF.Functions.Like(task.Title.ToLower(), pattern));
#pragma warning restore CA1304, CA1311
        }

        return tasks;
    }

    /// <summary>
    /// Applies the requested order, always with <c>Id</c> as a tiebreaker.
    /// </summary>
    /// <remarks>
    /// Id is unique and monotonic (version 7 GUIDs), so it gives every sort a
    /// stable total order. Without it, rows sharing a timestamp or a due date
    /// could appear twice or be skipped while paging
    /// (docs/API_CONVENTIONS.md).
    /// </remarks>
    private static IQueryable<TaskItem> ApplySort(IQueryable<TaskItem> tasks, TaskSort sort) =>
        sort switch
        {
            TaskSort.RecentlyCreated =>
                tasks.OrderByDescending(task => task.CreatedAt).ThenByDescending(task => task.Id),
            TaskSort.RecentlyUpdated =>
                tasks.OrderByDescending(task => task.UpdatedAt).ThenByDescending(task => task.Id),
            TaskSort.TitleAscending =>
                tasks.OrderBy(task => task.Title).ThenBy(task => task.Id),
            /*
              Undated tasks come after dated ones. PostgreSQL sorts nulls last
              ascending by default, but that is a database default rather than a
              promise, so the ordering is stated explicitly: an unscheduled task
              belongs below the ones someone has actually committed to a date.
            */
            _ =>
                tasks
                    .OrderBy(task => task.DueAt == null)
                    .ThenBy(task => task.DueAt)
                    .ThenBy(task => task.Id),
        };
}
