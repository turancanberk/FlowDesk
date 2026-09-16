using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Common;
using FlowDesk.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Customers.UpdateCustomer;

public sealed class UpdateCustomerHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public UpdateCustomerHandler(
        IFlowDeskDbContext dbContext,
        ITenantContext tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Result<CustomerDetail>> HandleAsync(
        Guid customerId,
        UpdateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ManageCustomers))
        {
            return Result.Failure<CustomerDetail>(
                TenancyErrors.InsufficientRole("Müşteri düzenlemek"));
        }

        // The global filter scopes this to the current workspace and to
        // non-archived rows, so a customer from another workspace simply is not
        // found here (ADR-0024).
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(candidate => candidate.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<CustomerDetail>(CustomerErrors.NotFound);
        }

        try
        {
            customer.Update(
                new CustomerDetails(
                    command.Name,
                    command.Email,
                    command.Phone,
                    command.Company,
                    command.Status,
                    command.Notes),
                _clock.UtcNow);
        }
        catch (DomainRuleViolationException)
        {
            return Result.Failure<CustomerDetail>(CustomerErrors.Archived);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(CustomerMapper.ToDetail(customer));
    }
}
