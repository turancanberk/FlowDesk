using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Customers.GetCustomer;

public sealed class GetCustomerHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetCustomerHandler(IFlowDeskDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<CustomerDetail>> HandleAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ViewCustomers))
        {
            return Result.Failure<CustomerDetail>(
                TenancyErrors.InsufficientRole("Müşteri görüntülemek"));
        }

        var tenantId = _tenantContext.TenantId;

        /*
          Archived customers are readable through their detail page — a link in
          an old ticket has to keep working. The filter is bypassed for that
          reason and the workspace condition re-applied explicitly.
        */
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(candidate => candidate.Id == customerId && candidate.TenantId == tenantId)
            .Select(candidate => new CustomerDetail(
                candidate.Id,
                candidate.Name,
                candidate.Email,
                candidate.Phone,
                candidate.Company,
                candidate.Status,
                candidate.Notes,
                candidate.ArchivedAt != null,
                candidate.CreatedAt,
                candidate.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return customer is null
            ? Result.Failure<CustomerDetail>(CustomerErrors.NotFound)
            : Result.Success(customer);
    }
}
