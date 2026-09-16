using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Common;
using FlowDesk.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Customers.ListCustomers;

/// <summary>
/// A page of customers, filtered and sorted.
/// </summary>
/// <remarks>
/// Composed as a single database query: the filters narrow it, the count and
/// the page are read from the same shape, and only the list columns are
/// projected. Notes in particular never leave the database here — a page of
/// twenty-five would otherwise carry up to a hundred thousand characters
/// nobody is going to read.
/// </remarks>
public sealed class ListCustomersHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ListCustomersHandler(IFlowDeskDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<PagedResult<CustomerListItem>>> HandleAsync(
        ListCustomersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ViewCustomers))
        {
            return Result.Failure<PagedResult<CustomerListItem>>(
                TenancyErrors.InsufficientRole("Müşterileri görüntülemek"));
        }

        var page = new PageRequest(query.Page, query.PageSize);
        var customers = BuildQuery(query);

        // Counted before paging, so the client can render "1–25 / 137".
        var totalCount = await customers.CountAsync(cancellationToken);

        var items = await ApplySort(customers, query.Sort)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(customer => new CustomerListItem(
                customer.Id,
                customer.Name,
                customer.Email,
                customer.Phone,
                customer.Company,
                customer.Status,
                customer.ArchivedAt != null,
                customer.CreatedAt,
                customer.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<CustomerListItem>(
            items,
            page.Page,
            page.PageSize,
            totalCount));
    }

    private IQueryable<Customer> BuildQuery(ListCustomersQuery query)
    {
        var tenantId = _tenantContext.TenantId;

        /*
          Including archived rows means bypassing the global filter, which also
          carries the workspace scope — so that scope is re-applied by hand.
          Getting this wrong would list every workspace's customers, which is
          why the two branches are written out rather than composed.
        */
        var customers = query.IncludeArchived
            ? _dbContext.Customers
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(customer => customer.TenantId == tenantId)
            : _dbContext.Customers.AsNoTracking();

        if (query.Status is { } status)
        {
            customers = customers.Where(customer => customer.Status == status);
        }

        var search = query.Search?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            /*
              Both sides are folded to ASCII lowercase before comparison. SQL
              LOWER() cannot fold Turkish correctly — the dotted and dotless i
              mean "YAZILIM" and "Yazılım" lowercase to different strings — so
              the comparison runs against a column the domain keeps folded
              (see TurkishText and Customer.SearchIndex).

              The upside beyond correctness: someone on a keyboard without
              Turkish characters can type "yazilim" and still find the record.
            */
            var pattern = $"%{TurkishText.Fold(search)}%";

            customers = customers.Where(customer =>
                EF.Functions.Like(customer.SearchIndex, pattern));
        }

        return customers;
    }

    /// <summary>
    /// Applies the requested order, always with <c>Id</c> as a tiebreaker.
    /// </summary>
    /// <remarks>
    /// None of the sort columns is unique. Without the tiebreaker, rows sharing
    /// a value could be returned in a different order on each page, which shows
    /// up as a record appearing twice or being skipped entirely while paging
    /// (docs/API_CONVENTIONS.md).
    /// </remarks>
    private static IQueryable<Customer> ApplySort(IQueryable<Customer> customers, CustomerSort sort) =>
        sort switch
        {
            CustomerSort.NameAscending =>
                customers.OrderBy(customer => customer.Name).ThenBy(customer => customer.Id),
            CustomerSort.NameDescending =>
                customers.OrderByDescending(customer => customer.Name).ThenBy(customer => customer.Id),
            CustomerSort.RecentlyCreated =>
                customers.OrderByDescending(customer => customer.CreatedAt).ThenBy(customer => customer.Id),
            _ =>
                customers.OrderByDescending(customer => customer.UpdatedAt).ThenBy(customer => customer.Id),
        };
}
